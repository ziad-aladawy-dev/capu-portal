import { useState, useRef, useCallback, useEffect } from "react";
import { FilesetResolver, FaceDetector } from "@mediapipe/tasks-vision";
import { PHOTO_SPECS, PHOTO_CHECK_KEYS, CHECK_CONFIG } from "../constants/photoSpecs";

const MODEL_DIR = "https://storage.googleapis.com/mediapipe-models/face_detector/blaze_face_short_range/float16/latest/";

function loadImage(file) {
  return new Promise((resolve, reject) => {
    const img = new Image();
    const url = URL.createObjectURL(file);
    img.onload = () => { URL.revokeObjectURL(url); resolve(img); };
    img.onerror = () => { URL.revokeObjectURL(url); reject(new Error("Failed to load image")); };
    img.src = url;
  });
}

function getCanvas(img) {
  const canvas = document.createElement("canvas");
  canvas.width = img.naturalWidth;
  canvas.height = img.naturalHeight;
  const ctx = canvas.getContext("2d");
  ctx.drawImage(img, 0, 0);
  return { canvas, ctx };
}

function computeLaplacianVariance(ctx, width, height) {
  const imageData = ctx.getImageData(0, 0, width, height);
  const data = imageData.data;
  let sum = 0;
  for (let y = 1; y < height - 1; y++) {
    for (let x = 1; x < width - 1; x++) {
      const idx = (y * width + x) * 4;
      const gray = 0.299 * data[idx] + 0.587 * data[idx + 1] + 0.114 * data[idx + 2];
      const top = 0.299 * data[((y - 1) * width + x) * 4] + 0.587 * data[((y - 1) * width + x) * 4 + 1] + 0.114 * data[((y - 1) * width + x) * 4 + 2];
      const bottom = 0.299 * data[((y + 1) * width + x) * 4] + 0.587 * data[((y + 1) * width + x) * 4 + 1] + 0.114 * data[((y + 1) * width + x) * 4 + 2];
      const left = 0.299 * data[(y * width + (x - 1)) * 4] + 0.587 * data[(y * width + (x - 1)) * 4 + 1] + 0.114 * data[(y * width + (x - 1)) * 4 + 2];
      const right = 0.299 * data[(y * width + (x + 1)) * 4] + 0.587 * data[(y * width + (x + 1)) * 4 + 1] + 0.114 * data[(y * width + (x + 1)) * 4 + 2];
      const laplacian = 4 * gray - top - bottom - left - right;
      sum += laplacian * laplacian;
    }
  }
  const n = (width - 2) * (height - 2);
  return n > 0 ? Math.sqrt(sum / n) : 0;
}

function computeAverageBrightness(ctx, width, height) {
  const imageData = ctx.getImageData(0, 0, width, height);
  const data = imageData.data;
  let sum = 0;
  for (let i = 0; i < data.length; i += 4) {
    sum += 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
  }
  const n = data.length / 4;
  return n > 0 ? sum / n : 0;
}

function computeBackgroundUniformity(ctx, width, height, faceBox) {
  const margin = 0.05;
  const regions = [];
  if (!faceBox) {
    regions.push(
      { x: 0, y: 0, w: width * margin, h: height },
      { x: width - width * margin, y: 0, w: width * margin, h: height },
      { x: 0, y: 0, w: width, h: height * margin },
      { x: 0, y: height - height * margin, w: width, h: height * margin },
    );
  } else {
    const left = Math.max(0, faceBox.x - width * margin);
    const top = Math.max(0, faceBox.y - height * margin);
    const right = Math.min(width, faceBox.x + faceBox.w + width * margin);
    const bottom = Math.min(height, faceBox.y + faceBox.h + height * margin);
    if (left > 0) regions.push({ x: 0, y: 0, w: left, h: height });
    if (right < width) regions.push({ x: right, y: 0, w: width - right, h: height });
    if (top > 0) regions.push({ x: 0, y: 0, w: width, h: top });
    if (bottom < height) regions.push({ x: 0, y: bottom, w: width, h: height - bottom });
  }
  if (regions.length === 0) return 0;
  const means = [];
  for (const r of regions) {
    const imageData = ctx.getImageData(r.x, r.y, r.w, r.h);
    const data = imageData.data;
    let sum = 0;
    for (let i = 0; i < data.length; i += 4) {
      sum += 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
    }
    means.push(sum / (data.length / 4));
  }
  const overallMean = means.reduce((a, b) => a + b, 0) / means.length;
  const variance = means.reduce((sum, m) => sum + (m - overallMean) ** 2, 0) / means.length;
  return Math.sqrt(variance) / (overallMean || 1);
}

function buildResult(checks) {
  const passed = checks.every((c) => c.passed);
  const total = checks.length;
  const passedCount = checks.filter((c) => c.passed).length;
  const score = total > 0 ? Math.round((passedCount / total) * 100) : 0;
  const hasBlockingFails = checks.some((c) => !c.passed && c.blocking);
  return { passed: passed && !hasBlockingFails, score, checks, hasBlockingFails };
}

export function usePhotoValidator() {
  const [modelLoaded, setModelLoaded] = useState(false);
  const [modelLoading, setModelLoading] = useState(false);
  const [modelError, setModelError] = useState(null);
  const [results, setResults] = useState(null);
  const [isProcessing, setIsProcessing] = useState(false);
  const [error, setError] = useState(null);

  const detectorRef = useRef(null);
  const loadAttempted = useRef(false);
  const processingRef = useRef(false);

  const specs = PHOTO_SPECS.profile;

  useEffect(() => {
    return () => {
      if (detectorRef.current) {
        detectorRef.current.close();
        detectorRef.current = null;
      }
    };
  }, []);

  const loadModel = useCallback(async () => {
    if (detectorRef.current) { setModelLoaded(true); return; }
    if (loadAttempted.current) return;
    loadAttempted.current = true;
    setModelLoading(true);
    setModelError(null);
    try {
      const wasmPath = new URL(
        "../../node_modules/@mediapipe/tasks-vision/wasm",
        window.location.href
      ).href;
      const vision = await FilesetResolver.forVisionTasks(wasmPath);
      const detector = await FaceDetector.createFromOptions(vision, {
        baseOptions: {
          modelAssetPath:
            "https://storage.googleapis.com/mediapipe-models/face_detector/blaze_face_short_range/float16/latest/face_detection_short_range.tflite",
        },
        runningMode: "IMAGE",
        minDetectionConfidence: specs.face.minDetectionConfidence,
      });
      detectorRef.current = detector;
      setModelLoaded(true);
    } catch (err) {
      setModelError(err.message || "Failed to load face detection model");
      loadAttempted.current = false;
    } finally {
      setModelLoading(false);
    }
  }, [specs]);

  const validate = useCallback(async (file) => {
    if (processingRef.current) return null;
    processingRef.current = true;
    setIsProcessing(true);
    setError(null);
    setResults(null);
    const run = [];

    try {
      const img = await loadImage(file);
      const { canvas, ctx } = getCanvas(img);
      const width = img.naturalWidth;
      const height = img.naturalHeight;

      const spec = PHOTO_SPECS.profile;

      run.push({
        key: PHOTO_CHECK_KEYS.FILE_TYPE,
        passed: spec.file.allowedTypes.includes(file.type),
        detail: file.type,
      });

      run.push({
        key: PHOTO_CHECK_KEYS.FILE_SIZE,
        passed: file.size <= spec.file.maxSizeMB * 1024 * 1024,
        detail: `${(file.size / (1024 * 1024)).toFixed(1)} MB`,
      });

      run.push({
        key: PHOTO_CHECK_KEYS.DIMENSIONS,
        passed: width >= spec.dimensions.minWidth && height >= spec.dimensions.minHeight,
        detail: `${width}×${height}`,
      });

      const aspectRatio = width / height;
      run.push({
        key: PHOTO_CHECK_KEYS.ASPECT_RATIO,
        passed: aspectRatio >= spec.dimensions.aspectRatio.min && aspectRatio <= spec.dimensions.aspectRatio.max,
        detail: `${width}×${height}`,
      });

      let faceBox = null;
      let faceDetection = null;

      if (detectorRef.current) {
        try {
          const detections = detectorRef.current.detect(canvas);
          faceDetection = detections;

          if (detections.detections && detections.detections.length > 0) {
            const d = detections.detections[0];
            const b = d.boundingBox;
            const score = d.categories?.[0]?.score ?? 0;
            faceBox = { x: b.originX, y: b.originY, w: b.width, h: b.height, score };

            const faceArea = b.width * b.height;
            const imageArea = width * height;
            const coverage = faceArea / imageArea;

            const faceCenterX = b.originX + b.width / 2;
            const faceCenterY = b.originY + b.height / 2;
            const imgCenterX = width / 2;
            const imgCenterY = height / 2;
            const offsetX = Math.abs(faceCenterX - imgCenterX) / width;
            const offsetY = Math.abs(faceCenterY - imgCenterY) / height;

            let tiltAngle = 0;
            const leftEye = d.keypoints?.find((k) => k.name === "leftEye");
            const rightEye = d.keypoints?.find((k) => k.name === "rightEye");
            if (leftEye && rightEye) {
              const dx = rightEye.x - leftEye.x;
              const dy = rightEye.y - leftEye.y;
              tiltAngle = Math.abs((Math.atan2(dy, dx) * 180) / Math.PI);
            }

            let eyeDistance = 0;
            if (leftEye && rightEye) {
              eyeDistance = Math.sqrt(
                (rightEye.x - leftEye.x) ** 2 + (rightEye.y - leftEye.y) ** 2
              );
            }

            run.push({
              key: PHOTO_CHECK_KEYS.SINGLE_FACE,
              passed: detections.detections.length === 1,
              detail: `${detections.detections.length} face(s)`,
            });

            run.push({
              key: PHOTO_CHECK_KEYS.FACE_CONFIDENCE,
              passed: score >= spec.face.minDetectionConfidence,
              detail: `${(score * 100).toFixed(0)}%`,
            });

            run.push({
              key: PHOTO_CHECK_KEYS.FACE_CENTERED,
              passed: offsetX <= spec.face.centerOffsetXMax && offsetY <= spec.face.centerOffsetYMax,
              detail: offsetX > offsetY
                ? `Horizontal offset ${(offsetX * 100).toFixed(0)}%`
                : `Vertical offset ${(offsetY * 100).toFixed(0)}%`,
            });

            run.push({
              key: PHOTO_CHECK_KEYS.FACE_SIZE,
              passed: coverage >= spec.face.coverageMin && coverage <= spec.face.coverageMax,
              detail: `${(coverage * 100).toFixed(0)}% of frame`,
            });

            run.push({
              key: PHOTO_CHECK_KEYS.HEAD_STRAIGHT,
              passed: tiltAngle <= spec.face.headTiltMaxDeg || (90 - tiltAngle) <= spec.face.headTiltMaxDeg,
              detail: tiltAngle <= 45
                ? `Tilted ${tiltAngle.toFixed(1)}°`
                : `Tilted ${(90 - tiltAngle).toFixed(1)}°`,
            });

            run.push({
              key: PHOTO_CHECK_KEYS.EYES_VISIBLE,
              passed: eyeDistance >= spec.face.minEyeDistancePx,
              detail: eyeDistance > 0
                ? `${eyeDistance.toFixed(0)}px apart`
                : "Eyes not detected clearly",
            });
          } else {
            run.push({ key: PHOTO_CHECK_KEYS.SINGLE_FACE, passed: false, detail: "0 faces" });
            run.push({ key: PHOTO_CHECK_KEYS.FACE_CONFIDENCE, passed: false, detail: "No face" });
            run.push({ key: PHOTO_CHECK_KEYS.FACE_CENTERED, passed: false, detail: "No face" });
            run.push({ key: PHOTO_CHECK_KEYS.FACE_SIZE, passed: false, detail: "No face" });
            run.push({ key: PHOTO_CHECK_KEYS.HEAD_STRAIGHT, passed: false, detail: "No face" });
            run.push({ key: PHOTO_CHECK_KEYS.EYES_VISIBLE, passed: false, detail: "No face" });
          }
        } catch {
          run.push({ key: PHOTO_CHECK_KEYS.SINGLE_FACE, passed: false, detail: "Detection error" });
          run.push({ key: PHOTO_CHECK_KEYS.FACE_CONFIDENCE, passed: false, detail: "Detection error" });
          run.push({ key: PHOTO_CHECK_KEYS.FACE_CENTERED, passed: false, detail: "Detection error" });
          run.push({ key: PHOTO_CHECK_KEYS.FACE_SIZE, passed: false, detail: "Detection error" });
          run.push({ key: PHOTO_CHECK_KEYS.HEAD_STRAIGHT, passed: false, detail: "Detection error" });
          run.push({ key: PHOTO_CHECK_KEYS.EYES_VISIBLE, passed: false, detail: "Detection error" });
        }
      } else {
        run.push({ key: PHOTO_CHECK_KEYS.SINGLE_FACE, passed: true, detail: "AI model loading" });
        run.push({ key: PHOTO_CHECK_KEYS.FACE_CONFIDENCE, passed: true, detail: "Skipped (model pending)" });
        run.push({ key: PHOTO_CHECK_KEYS.FACE_CENTERED, passed: true, detail: "Skipped (model pending)" });
        run.push({ key: PHOTO_CHECK_KEYS.FACE_SIZE, passed: true, detail: "Skipped (model pending)" });
        run.push({ key: PHOTO_CHECK_KEYS.HEAD_STRAIGHT, passed: true, detail: "Skipped (model pending)" });
        run.push({ key: PHOTO_CHECK_KEYS.EYES_VISIBLE, passed: true, detail: "Skipped (model pending)" });
      }

      const sharpness = computeLaplacianVariance(ctx, width, height);
      run.push({
        key: PHOTO_CHECK_KEYS.SHARPNESS,
        passed: sharpness >= spec.quality.minSharpness,
        detail: `Score: ${sharpness.toFixed(0)}`,
      });

      const brightness = computeAverageBrightness(ctx, width, height);
      run.push({
        key: PHOTO_CHECK_KEYS.BRIGHTNESS,
        passed: brightness >= spec.quality.minBrightness && brightness <= spec.quality.maxBrightness,
        detail: brightness < 128 ? "Too dark" : "Good",
        detailAr: undefined,
      });

      const uniformity = computeBackgroundUniformity(ctx, width, height, faceBox);
      run.push({
        key: PHOTO_CHECK_KEYS.BACKGROUND,
        passed: uniformity <= spec.quality.backgroundUniformityMax,
        detail: uniformity <= spec.quality.backgroundUniformityMax ? "Uniform" : "Uneven",
      });

      const configMap = {};
      for (const c of CHECK_CONFIG) configMap[c.key] = c;

      const finalChecks = run.map((r) => ({
        ...r,
        blocking: configMap[r.key]?.blocking ?? false,
        labelKey: configMap[r.key]?.labelKey ?? r.key,
      }));

      const result = buildResult(finalChecks);
      setResults(result);
      setIsProcessing(false);
      processingRef.current = false;
      return result;
    } catch (err) {
      setError(err.message || "Validation failed");
      setIsProcessing(false);
      processingRef.current = false;
      return null;
    }
  }, []);

  const reset = useCallback(() => {
    setResults(null);
    setError(null);
    setIsProcessing(false);
    processingRef.current = false;
  }, []);

  return {
    modelLoaded,
    modelLoading,
    modelError,
    loadModel,
    validate,
    results,
    isProcessing,
    error,
    reset,
  };
}
