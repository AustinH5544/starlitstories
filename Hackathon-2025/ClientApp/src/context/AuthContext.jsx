import React, { createContext, useContext, useState, useEffect } from 'react';
import api from "../api";

const AuthContext = createContext();

export const AuthProvider = ({ children }) => {
    const [user, setUser] = useState(null);
    const [token, setToken] = useState(localStorage.getItem("token"));

    const login = ({ token: jwt, email, membership, profileImage, name }) => {
        setUser({ email, membership, profileImage, name });
        setToken(jwt);
        localStorage.setItem("token", jwt);
    };

    const logout = () => {
        setUser(null);
        setToken(null);
        localStorage.removeItem("token");
    };

    useEffect(() => {
        if (!token) return;
        (async () => {
            try {
                // Skip auto-logout redirect if this returns 401 so we can handle it gracefully here
                const { data } = await api.get("profile/me", { skipAuth401Handler: true });
                setUser({
                    email: data.email,
                    membership: data.membership,
                    name: data.name,
                    profileImage: data.profileImage,
                });
            } catch (e) {
                console.error("Failed to load profile", e);
                // Optional: if unauthorized, clear token and go to login
                localStorage.removeItem("token");
                setToken(null);
                // window.location.href = "/login";
            }
        })();
    }, [token]);

    return (
        <AuthContext.Provider value={{ user, setUser, token, login, logout }}>
            {children}
        </AuthContext.Provider>
    );
};

export const useAuth = () => useContext(AuthContext);
