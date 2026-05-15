export const searchTree = (
    node,
    searchTerm
  ) => {
    if (!searchTerm.trim()) {
      return {
        visible: true,
        matchedIds: [],
      };
    }
  
    const matchedIds = [];
  
    const term =
      searchTerm.toLowerCase();
  
    const traverse = (currentNode) => {
      let matched = false;
  
      if (
        currentNode.name
          .toLowerCase()
          .includes(term)
      ) {
        matched = true;
  
        matchedIds.push(currentNode.id);
      }
  
      currentNode.children.forEach(
        (child) => {
          const childMatched =
            traverse(child);
  
          if (childMatched) {
            matched = true;
          }
        }
      );
  
      return matched;
    };
  
    const visible = traverse(node);
  
    return {
      visible,
      matchedIds,
    };
  };