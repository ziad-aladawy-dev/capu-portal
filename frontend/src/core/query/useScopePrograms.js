import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import * as structureService from "../services/structureService";
import { normalizeType } from "../../modules/university/utils/nodeTypeRegistry";

/**
 * Recursively collect every Program node inside a structure subtree.
 * The API returns a nested StructureNodeDto ({ id, name, localizedName, type,
 * children }); a program can sit directly under a Faculty OR under a System
 * under a Faculty, so we walk the whole subtree rather than just direct children.
 */
function collectPrograms(node, acc = []) {
  if (!node) return acc;
  if (normalizeType(node.type) === "Program") {
    acc.push({ id: node.id, name: node.name, localizedName: node.localizedName });
  }
  for (const child of node.children || []) collectPrograms(child, acc);
  return acc;
}

/**
 * Resolves the set of academic programs the academic-plans page should expose
 * for the currently selected navbar scope node:
 *   - scope IS a Program        → just that program (shown immediately)
 *   - scope CONTAINS programs    → every descendant program (user picks one)
 *   - no scope selected          → all programs (legacy browse-everything mode)
 */
export function useScopePrograms(scopeNode) {
  const typeLabel = scopeNode ? normalizeType(scopeNode.type) : null;
  const isProgram = typeLabel === "Program";

  // Container scope (Faculty/System/University/…): walk its subtree.
  const subtreeQuery = useQuery({
    queryKey: ["structure-subtree", scopeNode?.id],
    queryFn: () => structureService.fetchStructureSubtree(scopeNode.id),
    enabled: !!scopeNode && !isProgram,
    staleTime: 5 * 60 * 1000,
    retry: false,
  });

  // No scope selected: fall back to the flat program lookup.
  const allProgramsQuery = useQuery({
    queryKey: ["structure-programs-all"],
    queryFn: () => structureService.fetchPrograms(),
    enabled: !scopeNode,
    staleTime: 5 * 60 * 1000,
    retry: false,
  });

  const programs = useMemo(() => {
    if (!scopeNode) {
      const list = allProgramsQuery.data;
      return Array.isArray(list) ? list : [];
    }
    if (isProgram) {
      return [{ id: scopeNode.id, name: scopeNode.name, localizedName: scopeNode.name }];
    }
    return collectPrograms(subtreeQuery.data);
  }, [scopeNode, isProgram, allProgramsQuery.data, subtreeQuery.data]);

  const isLoading =
    (!scopeNode && allProgramsQuery.isLoading) ||
    (!!scopeNode && !isProgram && subtreeQuery.isLoading);

  return {
    programs,
    isLoading,
    isProgram,
    hasScope: !!scopeNode,
  };
}
