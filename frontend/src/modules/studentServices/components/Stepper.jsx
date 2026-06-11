import React from "react";
import "../styles/components/Stepper.css";

const Stepper = ({ steps = [], currentStep = 0 }) => {
  return (
    <div className="stepper">
      {steps.map((step, index) => (
        <div key={index} className={`stepper-step ${index <= currentStep ? "active" : ""} ${index < currentStep ? "completed" : ""}`}>
          <div className="stepper-circle">{index < currentStep ? "✓" : index + 1}</div>
          <span className="stepper-label">{step}</span>
          {index < steps.length - 1 && <div className="stepper-line" />}
        </div>
      ))}
    </div>
  );
};

export default Stepper;