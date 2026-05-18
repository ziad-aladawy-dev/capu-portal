export const getNodeTypeLabel = (typeValue) => {
    const typeMap = {
      1: "University",
      2: "Faculty",
      3: "System",
      4: "Program",
      5: "Level",
      6: "Department",
      7: "Specialization",
    };
    return typeMap[typeValue] || (typeof typeValue === "string" ? typeValue : "Unknown");
  };
  
  export const normalizeType = (type) => {
    if (typeof type === "string") return type;
    return getNodeTypeLabel(type);
  };
  
  export const getNodeTypeValue = (typeLabel) => {
    const reverseMap = {
      University: 1,
      Faculty: 2,
      System: 3,
      Program: 4,
      Level: 5,
      Department: 6,
      Specialization: 7,
    };
    return reverseMap[typeLabel] || 4;
  };
  
  export const getAllowedChildTypes = (parentType) => {
    const parentTypeStr = normalizeType(parentType);
    const rules = {
      University: ["Faculty"],
      Faculty: ["System", "Program"],
      System: ["Program"],
      Program: ["Level", "Specialization"],
      Level: [],
      Department: [],
      Specialization: ["Level"],
    };
    return rules[parentTypeStr] || [];
  };
  
  export const canMoveToParent = (nodeType, targetParentType) => {
    if (!targetParentType) {
      return normalizeType(nodeType) === "University";
    }
    const allowed = getAllowedChildTypes(targetParentType);
    return allowed.includes(normalizeType(nodeType));
  };