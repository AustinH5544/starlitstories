"use client"

import { useState, useEffect } from "react"
import { Link, useLocation } from "react-router-dom"
import { useAuth } from "../context/AuthContext"
import "./NavBar.css"

const NavBar = () => {
    const { user, logout } = useAuth()
    const location = useLocation()
    const [scrolled, setScrolled] = useState(false)
    const [mobileMenuOpen, setMobileMenuOpen] = useState(false)
    const BASE = import.meta.env.BASE_URL;
    const avatarFile = user?.profileImage || localStorage.getItem("avatar") || null;
    const navAvatarSrc = avatarFile
        ? (avatarFile.startsWith("http")
            ? avatarFile
            : `${BASE}avatars/${avatarFile}`)
        : `${BASE}avatars/default-avatar.png`;

    // Handle scroll effect for navbar
    useEffect(() => {
        const handleScroll = () => {
            const isScrolled = window.scrollY > 10
            if (isScrolled !== scrolled) {
                setScrolled(isScrolled)
            }
        }

        window.addEventListener("scroll", handleScroll)
        return () => window.removeEventListener("scroll", handleScroll)
    }, [scrolled])

    // Close mobile menu when changing routes
    useEffect(() => {
        setMobileMenuOpen(false)
    }, [location])

    const toggleMobileMenu = () => {
        setMobileMenuOpen(!mobileMenuOpen)
    }

    const handleHomeClick = (e) => {
        if (location.pathname === "/") {
            e.preventDefault();
            window.scrollTo({ top: 0, behavior: "smooth" });
            setMobileMenuOpen(false);
        }
    };

    return (
        <nav className={`navbar ${scrolled ? "scrolled" : ""}`}>
            <div className="nav-container">
                <div className="nav-left">
                    <Link to="/" className="logo-link">
                        <span className="logo">Starlit Stories</span>
                        <span className="logo-icon">✨</span>
                    </Link>
                </div>

                <div className={`nav-center ${mobileMenuOpen ? "mobile-open" : ""}`}>
                    {user && (
                        <div className="mobile-user-avatar">
                            <Link to="/profile">
                                <img
                                    src={navAvatarSrc}
                                    alt="Profile"
                                    onError={(e) => (e.currentTarget.src = `${BASE}avatars/default-avatar.png`)}
                                />
                                <div className="mobile-user-name">
                                    {user.username || (user.email?.split("@")[0]) || "My Account"}
                                </div>
                            </Link>
                        </div>
                    )}

                    <Link
                        to="/"
                        className={location.pathname === "/" ? "active" : ""}
                        onClick={handleHomeClick}
                    >
                        Home
                    </Link>
                    {user && (
                        <Link to="/create" className={location.pathname === "/create" ? "active" : ""}>
                            Create Story
                        </Link>
                    )}
                    <Link to="/about" className={location.pathname === "/about" ? "active" : ""}>
                        About
                    </Link>
                    {/*<a href="/#how-it-works" className="nav-link">*/}
                    {/*    How It Works*/}
                    {/*</a>*/}
                </div>

                <div className="nav-right">
                    {user ? (
                        <div className="user-menu">
                            <a href="/profile" className="profile-link">
                                <div className="user-avatar">
                                    <img
                                        src={navAvatarSrc}
                                        alt="Profile"
                                        style={{ width: "100%", height: "100%", borderRadius: "50%", objectFit: "cover" }}
                                        onError={(e) => (e.currentTarget.style.display = "none")} // letter fallback shows if image fails
                                    />
                                </div>
                                <span className="user-name">
                                    {user.username || (user.email?.split("@")[0]) || "My Account"}
                                </span>
                            </a>
                            <button className="logout-button" onClick={logout}>Logout</button>
                        </div>
                    ) : (
                        <>
                            <a href="/login" className="login-button">Login</a>
                            <a href="/signup" className="signup-button">Sign Up</a>
                        </>
                    )}
                </div>

                <button className="mobile-menu-toggle" onClick={toggleMobileMenu}>
                    <div className={`hamburger ${mobileMenuOpen ? "open" : ""}`}>
                        <span></span>
                        <span></span>
                        <span></span>
                    </div>
                </button>
            </div>
        </nav>
    )
}

export default NavBar