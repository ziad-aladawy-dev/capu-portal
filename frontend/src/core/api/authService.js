import axiosInstance from './axiosConfig';

class AuthService {
  async login(credentials) {
    try {
      const response = await axiosInstance.post('/Auth/login/staff', credentials);
      
      if (response.data && response.data.token) {
        localStorage.setItem('authToken', response.data.token);
        localStorage.setItem('user', JSON.stringify({
          userId: response.data.userId,
          name: response.data.name,
          email: response.data.email,
          userType: response.data.userType
        }));
      }
      
      return response.data;
    } catch (error) {
      throw new Error(error.userMessage || 'Login failed');
    }
  }

  logout() {
    localStorage.removeItem('authToken');
    localStorage.removeItem('user');
    window.location.href = '/login';
  }

  getToken() {
    return localStorage.getItem('authToken');
  }

  getCurrentUser() {
    const userStr = localStorage.getItem('user');
    if (userStr) {
      return JSON.parse(userStr);
    }
    return null;
  }

  isAuthenticated() {
    return !!this.getToken();
  }
}

export default new AuthService();