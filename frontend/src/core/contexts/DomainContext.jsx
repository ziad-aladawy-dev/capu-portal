import PropTypes from "prop-types";
import { createContext, useContext } from "react";

const DomainContext = createContext();

export const DomainProvider = ({ children }) => {
  return (
    <DomainContext.Provider
      value={{
        selectedDomain: {
          name: "Capital University",
        },
      }}
    >
      {children}
    </DomainContext.Provider>
  );
};

export const useDomain = () =>
  useContext(DomainContext);

DomainProvider.propTypes = {
  children: PropTypes.node,
};