// Mock auth service that mimics a backend login
const authService = {
  login: async (credentials) => {
    // Simulate network delay
    await new Promise(resolve => setTimeout(resolve, 500));

    if (credentials.nationalId === 'admin' && credentials.password === 'admin123') {
      const user = {
        id: '1',
        nationalId: 'admin',
        name: 'Admin User',
        role: 'SuperAdmin'
      };
      localStorage.setItem('user', JSON.stringify(user));
      localStorage.setItem('token', 'mock-jwt-token');
      return { user, token: 'mock-jwt-token' };
    } else {
      throw new Error('Invalid credentials');
    }
  },

  logout: () => {
    localStorage.removeItem('user');
    localStorage.removeItem('token');
  },

  getCurrentUser: () => {
    const userStr = localStorage.getItem('user');
    if (!userStr) return null;
    try {
      return JSON.parse(userStr);
    } catch (e) {
      return null;
    }
  },

  isAuthenticated: () => {
    return !!localStorage.getItem('token');
  }
};

export default authService;
