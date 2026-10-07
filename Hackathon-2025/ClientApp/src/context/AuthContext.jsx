import React, { createContext, useContext, useState, useEffect } from 'react';
import api from "../api";
import posthog from '../analytics';

export const AuthContext = createContext();

export const AuthProvider = ({ children }) => {
    const [user, setUser] = useState(null);
    const [token, setToken] = useState(typeof window !== 'undefined' ? localStorage.getItem("token") : null);

    // EXPECTS backend to return { token, email, username, membership, profileImage?, isAdmin? }
    const login = ({ token: jwt, email, username, membership, profileImage, isAdmin }) => {
        setUser({ email, username, membership, profileImage, isAdmin: Boolean(isAdmin) });
        setToken(jwt);
        localStorage.setItem("token", jwt);
    };

    const logout = () => {
        posthog.reset();
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
                    isAdmin: data.isAdmin ?? u?.isAdmin ?? false,
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
