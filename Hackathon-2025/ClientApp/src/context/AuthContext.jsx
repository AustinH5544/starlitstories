import React, { createContext, useContext, useState, useEffect } from 'react';
import api from "../api";

const AuthContext = createContext();

export const AuthProvider = ({ children }) => {
    const [user, setUser] = useState(null);
    const [token, setToken] = useState(localStorage.getItem("token"));

    // EXPECTS backend to return { token, email, username, membership, profileImage? }
    const login = ({ token: jwt, email, username, membership, profileImage }) => {
        setUser({ email, username, membership, profileImage });
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
                // Rehydrate on reload: backend /api/profile/me should include username now
                const { data } = await api.get("profile/me", { skipAuth401Handler: true });
                setUser((u) => ({
                    ...u,
                    email: data.email ?? u?.email,
                    username: data.username ?? u?.username,
                    membership: data.membership ?? u?.membership,
                    profileImage: data.profileImage ?? u?.profileImage,
                }));
            } catch (e) {
                console.error("Failed to load profile", e);
                localStorage.removeItem("token");
                setToken(null);
                // window.location.href = "/login"; // optional
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
