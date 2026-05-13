const staticTree = {
    id: 1,
    name: 'Capital University',
    type: 'University',
    children: [
      {
        id: 2,
        name: 'Faculty of Engineering',
        type: 'Faculty',
        children: [
          {
            id: 3,
            name: 'Computer Science',
            type: 'Department',
            children: [
              {
                id: 4,
                name: 'Level 1',
                type: 'Level',
                children: [],
              },
            ],
          },
        ],
      },
    ],
  };
  
  export default staticTree;