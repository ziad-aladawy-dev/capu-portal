const normalizedTree = {
    rootId: 1,
  
    nodes: {
      1: {
        id: 1,
        name: 'Capital University',
        type: 'University',
        parentId: null,
        childrenIds: [2, 3],
        path: '1',
        depth: 0,
        order: 1,
      },
  
      2: {
        id: 2,
        name: 'Faculty of Engineering',
        type: 'Faculty',
        parentId: 1,
        childrenIds: [4],
        path: '1/2',
        depth: 1,
        order: 1,
      },
  
      3: {
        id: 3,
        name: 'Faculty of Business',
        type: 'Faculty',
        parentId: 1,
        childrenIds: [],
        path: '1/3',
        depth: 1,
        order: 2,
      },
  
      4: {
        id: 4,
        name: 'Computer Science',
        type: 'Department',
        parentId: 2,
        childrenIds: [5],
        path: '1/2/4',
        depth: 2,
        order: 1,
      },
  
      5: {
        id: 5,
        name: 'Artificial Intelligence',
        type: 'Program',
        parentId: 4,
        childrenIds: [],
        path: '1/2/4/5',
        depth: 3,
        order: 1,
      },
    },
  };
  
  export default normalizedTree;