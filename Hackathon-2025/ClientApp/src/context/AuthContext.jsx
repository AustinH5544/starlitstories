import React, { createContext, useContext, useState, useEffect } from 'react';
// import api from "../api" // <-- optional if you want auto-hydration shown below

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

    // OPTIONAL: hydrate user after refresh using the token
    // useEffect(() => {
    //   if (!token) return;
    //   (async () => {
    //     try {
    //       const { data } = await api.get("profile/me");
    //       setUser({
    //         email: data.email,
    //         membership: data.membership,
    //         profileImage: data.profileImage,
    //         name: data.name,
    //       });
    //     } catch (e) {
    //       console.error("Failed to load profile", e);
    //     }
    //   })();
    // }, [token]);

    return (
        <AuthContext.Provider value={{ user, setUser, token, login, logout }}>
            {children}
        </AuthContext.Provider>
    );
};

export const useAuth = () => useContext(AuthContext);
