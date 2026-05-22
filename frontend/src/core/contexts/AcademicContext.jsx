import PropTypes from "prop-types";
import { createContext, useContext } from "react";

const AcademicContext = createContext();

export const AcademicProvider = ({
  children,
}) => {
  return (
    <AcademicContext.Provider
      value={{
        selectedYear: "2025-2026",
        selectedSemester: "Fall Semester",
      }}
    >
      {children}
    </AcademicContext.Provider>
  );
};

export const useAcademic = () =>
  useContext(AcademicContext);

AcademicProvider.propTypes = {
  children: PropTypes.node,
};