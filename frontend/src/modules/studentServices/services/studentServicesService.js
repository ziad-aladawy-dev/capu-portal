import apiClient from "../../../core/api/apiClient";

const BASE_URL = "/student-services";

// ---------------------- Services (CRUD) ----------------------
export const getServices = async () => {
  const response = await apiClient.get(`${BASE_URL}/services`);
  return response.data;
};

export const getServiceById = async (id) => {
  const response = await apiClient.get(`${BASE_URL}/services/${id}`);
  return response.data;
};

export const getAllServices = async () => {
  const response = await apiClient.get(`${BASE_URL}/services/all`);
  return response.data;
};

export const createService = async (data) => {
  const response = await apiClient.post(`${BASE_URL}/services`, data);
  return response.data;
};

export const updateService = async (id, data) => {
  const response = await apiClient.put(`${BASE_URL}/services/${id}`, data);
  return response.data;
};

export const deleteService = async (id) => {
  const response = await apiClient.delete(`${BASE_URL}/services/${id}`);
  return response.data;
};

export const toggleServiceStatus = async (id) => {
  const response = await apiClient.patch(`${BASE_URL}/services/${id}/toggle`);
  return response.data;
};

export const getAvailableServicesForStudent = async (studentId) => {
  const response = await apiClient.get(`${BASE_URL}/services/available`, {
    params: { studentId },
  });
  return response.data;
};

// ---------------------- Workflows ----------------------
export const getWorkflows = async () => {
  const response = await apiClient.get(`${BASE_URL}/workflows`);
  return response.data;
};

export const getWorkflowById = async (id) => {
  const response = await apiClient.get(`${BASE_URL}/workflows/${id}`);
  return response.data;
};

export const createWorkflow = async (data) => {
  const response = await apiClient.post(`${BASE_URL}/workflows`, data);
  return response.data;
};

export const updateWorkflow = async (id, data) => {
  const response = await apiClient.put(`${BASE_URL}/workflows/${id}`, data);
  return response.data;
};

export const deleteWorkflow = async (id) => {
  const response = await apiClient.delete(`${BASE_URL}/workflows/${id}`);
  return response.data;
};

export const addWorkflowStep = async (workflowId, stepData) => {
  const response = await apiClient.post(`${BASE_URL}/workflows/${workflowId}/steps`, stepData);
  return response.data;
};

export const updateWorkflowStep = async (stepId, stepData) => {
  const response = await apiClient.put(`${BASE_URL}/workflows/steps/${stepId}`, stepData);
  return response.data;
};

export const deleteWorkflowStep = async (stepId) => {
  const response = await apiClient.delete(`${BASE_URL}/workflows/steps/${stepId}`);
  return response.data;
};

// ---------------------- Student Requests ----------------------
export const getStudentRequests = async () => {
  const response = await apiClient.get(`${BASE_URL}/requests`);
  return response.data;
};

export const getStudentRequestById = async (id) => {
  const response = await apiClient.get(`${BASE_URL}/requests/${id}`);
  return response.data;
};

export const createDraftRequest = async (serviceId) => {
  const response = await apiClient.post(`${BASE_URL}/requests`, null, {
    params: { serviceId },
  });
  return response.data;
};

export const saveStepData = async (requestId, stepKey, data) => {
  const response = await apiClient.post(`${BASE_URL}/requests/${requestId}/step`, {
    stepKey,
    data,
  });
  return response.data;
};

export const submitRequest = async (requestId) => {
  const response = await apiClient.post(`${BASE_URL}/requests/${requestId}/submit`);
  return response.data;
};

export const processPayment = async (requestId, paymentMethod) => {
  const response = await apiClient.post(`${BASE_URL}/requests/${requestId}/payment`, {
    paymentMethod,
  });
  return response.data;
};

// ---------------------- Staff Requests ----------------------
export const getAllRequests = async () => {
  const response = await apiClient.get(`${BASE_URL}/requests/all`);
  return response.data;
};

export const assignRequest = async (requestId, staffId) => {
  const response = await apiClient.post(`${BASE_URL}/requests/${requestId}/assign`, {
    staffId,
  });
  return response.data;
};

export const updateRequestStatus = async (requestId, newStatus, comment) => {
  const response = await apiClient.post(`${BASE_URL}/requests/${requestId}/status`, {
    newStatus,
    comment,
  });
  return response.data;
};

export const addComment = async (requestId, comment) => {
  const response = await apiClient.post(`${BASE_URL}/requests/${requestId}/comment`, {
    comment,
  });
  return response.data;
};

export const getAssignedToMe = async () => {
  const response = await apiClient.get(`${BASE_URL}/requests/assigned-to-me`);
  return response.data;
};

export const getPagedStaffRequests = async (page, pageSize, search, sortBy, ascending) => {
  const params = { page, pageSize, search, sortBy, ascending };
  const response = await apiClient.get(`${BASE_URL}/requests/staff/paged`, { params });
  return response.data;
};

// ---------------------- Statistics ----------------------
export const getStaffStatistics = async () => {
  const response = await apiClient.get(`${BASE_URL}/statistics/staff`);
  return response.data;
};

export const getMyStudentStatistics = async () => {
  const response = await apiClient.get(`${BASE_URL}/statistics/student/me`);
  return response.data;
};

export const getStudentStatisticsById = async (studentId) => {
  const response = await apiClient.get(`${BASE_URL}/statistics/student/${studentId}`);
  return response.data;
};

export const getRecentRequests = async (count = 5) => {
  const response = await apiClient.get(`${BASE_URL}/statistics/staff/recent-requests`, { params: { count } });
  return response.data;
};

// ---------------------- File Upload ----------------------
export const uploadFile = async (requestId, stepKey, file) => {
  const formData = new FormData();
  formData.append("file", file);
  const response = await apiClient.post(
    `${BASE_URL}/upload/request/${requestId}/step/${stepKey}`,
    formData,
    { headers: { "Content-Type": "multipart/form-data" } }
  );
  return response.data;
};

export const downloadFile = async (attachmentId) => {
  const response = await apiClient.get(`${BASE_URL}/upload/attachment/${attachmentId}`, {
    responseType: "blob",
  });
  return response.data;
};

export const deleteFile = async (attachmentId) => {
  const response = await apiClient.delete(`${BASE_URL}/upload/attachment/${attachmentId}`);
  return response.data;
};

// ---------------------- External APIs (Structure, Years, Semesters) ----------------------
export const getAcademicYears = async () => {
  const response = await apiClient.get("/academic-years");
  return response.data;
};

export const getSemestersByYear = async (academicYearId) => {
  const response = await apiClient.get(`/academic-years/${academicYearId}/semesters`);
  return response.data;
};

export const getFaculties = async () => {
  const response = await apiClient.get("/structure/lookups/faculties");
  return response.data;
};

export const getProgramsByFaculty = async (facultyId) => {
  const response = await apiClient.get(`/structure/lookups/faculties/${facultyId}/programs`);
  return response.data;
};

// ---------------------- Attachments & Pending Request ----------------------
export const getStudentRequestAttachments = async (requestId) => {
  const response = await apiClient.get(`${BASE_URL}/requests/${requestId}/attachments`);
  return response.data;
};

export const getPendingRequestForService = async (serviceId) => {
  const response = await apiClient.get(`${BASE_URL}/requests/pending/${serviceId}`);
  return response.data;
};