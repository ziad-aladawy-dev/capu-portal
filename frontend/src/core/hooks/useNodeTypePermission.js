import { usePermission } from "../auth/usePermission";
import { getNodeTypePermission } from "../../modules/university/utils/nodeTypeRegistry";

export function useNodeTypePermission() {
  const { can } = usePermission();

  const canModify = (typeValue, action) => {
    const perTypePerm = getNodeTypePermission(typeValue, action);
    if (perTypePerm && can(perTypePerm, 1)) return true;

    const levelMap = { view: 1, insert: 2, edit: 3, delete: 5 };
    const level = levelMap[action] || 1;
    return can("structure.structure", level);
  };

  return { canModify };
}
