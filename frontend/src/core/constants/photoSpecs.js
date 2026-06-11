export const PHOTO_CHECK_KEYS = {
  FILE_TYPE: "file_type",
  FILE_SIZE: "file_size",
  DIMENSIONS: "dimensions",
  ASPECT_RATIO: "aspect_ratio",
  SINGLE_FACE: "single_face",
  FACE_CONFIDENCE: "face_confidence",
  FACE_CENTERED: "face_centered",
  FACE_SIZE: "face_size",
  HEAD_STRAIGHT: "head_straight",
  EYES_VISIBLE: "eyes_visible",
  SHARPNESS: "sharpness",
  BRIGHTNESS: "brightness",
  BACKGROUND: "background",
};

export const PHOTO_SPECS = {
  profile: {
    file: {
      maxSizeMB: 5,
      allowedTypes: ["image/jpeg", "image/png", "image/webp"],
    },
    dimensions: {
      minWidth: 300,
      minHeight: 300,
      aspectRatio: { min: 0.75, max: 1.5 },
    },
    face: {
      minDetectionConfidence: 0.7,
      exactlyOne: true,
      coverageMin: 0.20,
      coverageMax: 0.75,
      centerOffsetXMax: 0.20,
      centerOffsetYMax: 0.25,
      headTiltMaxDeg: 18,
      minEyeDistancePx: 30,
      eyesVisible: true,
    },
    quality: {
      minSharpness: 40,
      minBrightness: 50,
      maxBrightness: 230,
      backgroundUniformityMax: 0.30,
    },
  },
};

export const CHECK_CONFIG = [
  {
    key: PHOTO_CHECK_KEYS.FILE_TYPE,
    labelKey: "photo_check_file_type",
    blocking: true,
    group: "file",
  },
  {
    key: PHOTO_CHECK_KEYS.FILE_SIZE,
    labelKey: "photo_check_file_size",
    blocking: true,
    group: "file",
  },
  {
    key: PHOTO_CHECK_KEYS.DIMENSIONS,
    labelKey: "photo_check_dimensions",
    blocking: true,
    group: "file",
  },
  {
    key: PHOTO_CHECK_KEYS.ASPECT_RATIO,
    labelKey: "photo_check_aspect_ratio",
    blocking: false,
    group: "file",
  },
  {
    key: PHOTO_CHECK_KEYS.SINGLE_FACE,
    labelKey: "photo_check_single_face",
    blocking: true,
    group: "face",
  },
  {
    key: PHOTO_CHECK_KEYS.FACE_CONFIDENCE,
    labelKey: "photo_check_face_detected",
    blocking: true,
    group: "face",
  },
  {
    key: PHOTO_CHECK_KEYS.FACE_CENTERED,
    labelKey: "photo_check_face_centered",
    blocking: false,
    group: "face",
  },
  {
    key: PHOTO_CHECK_KEYS.FACE_SIZE,
    labelKey: "photo_check_face_size",
    blocking: true,
    group: "face",
  },
  {
    key: PHOTO_CHECK_KEYS.HEAD_STRAIGHT,
    labelKey: "photo_check_head_straight",
    blocking: false,
    group: "face",
  },
  {
    key: PHOTO_CHECK_KEYS.EYES_VISIBLE,
    labelKey: "photo_check_eyes_visible",
    blocking: false,
    group: "face",
  },
  {
    key: PHOTO_CHECK_KEYS.SHARPNESS,
    labelKey: "photo_check_sharpness",
    blocking: true,
    group: "quality",
  },
  {
    key: PHOTO_CHECK_KEYS.BRIGHTNESS,
    labelKey: "photo_check_brightness",
    blocking: false,
    group: "quality",
  },
  {
    key: PHOTO_CHECK_KEYS.BACKGROUND,
    labelKey: "photo_check_background",
    blocking: false,
    group: "quality",
  },
];
