import axios from 'axios';

const api = axios.create({
    baseURL: '/api',
    timeout: 500000,
    headers: { 'Content-Type': 'application/json' }
});

// Attach token before requests
api.interceptors.request.use((config) => {
    const token = localStorage.getItem("token");
    if (token) {
        config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
});

//  Auto-logout on 401 response
api.interceptors.response.use(
    res => res,
    err => {
        if (err.response?.status === 401) {
            // Clear token
            localStorage.removeItem("token");
            // Optional: clear any persisted user data here

            // Force redirect to login page
            window.location.href = "/login";
        }
        return Promise.reject(err);
    }
);

export default api;