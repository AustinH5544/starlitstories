// src/api.js
import axios from 'axios';

const api = axios.create({
    baseURL: '/api', // proxy handles localhost:5001
    timeout: 500000,
    headers: { 'Content-Type': 'application/json' }
});

export default api;