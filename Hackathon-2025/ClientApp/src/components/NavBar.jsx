"use client"

import { useState, useEffect } from "react"
import { Link, useLocation } from "react-router-dom"
import { useAuth } from "../context/AuthContext"
import { createPortal } from "react-dom";
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

    // Lock/unlock page scroll + enable overlay when drawer is open
    useEffect(() => {
        const root = document.documentElement;
        const body = document.body;
        if (mobileMenuOpen) {
            root.classList.add("drawer-open");
            body.classList.add("drawer-open");
        } else {
            root.classList.remove("drawer-open");
            body.classList.remove("drawer-open");
        }
        return () => {
            root.classList.remove("drawer-open");
            body.classList.remove("drawer-open");
        };
    }, [mobileMenuOpen]);

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

    const handleAboutClick = (e) => {
        if (location.pathname === "/about") {
            e.preventDefault();
            window.scrollTo({ top: 0, behavior: "smooth" });
            setMobileMenuOpen(false);
        }
    };

    return (
        <>
        {mobileMenuOpen &&
          createPortal(
            <div className="drawer-overlay" onClick={() => setMobileMenuOpen(false)} />,
            document.body
          )
        }
        <nav className={`navbar ${scrolled ? "scrolled" : ""}`}>
            <div className="nav-container">
                <div className="nav-left">
                    <Link to="/" className="logo-link">
                        <span className="logo">Starlit Stories</span>
                        <span className="logo-icon">✨</span>
                    </Link>
                </div>

                <div className={`nav-center ${mobileMenuOpen ? "mobile-open" : ""}`}>
                    {user ? (
                        <>
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

                            <Link to="/" onClick={handleHomeClick}
                                className={location.pathname === "/" ? "active" : ""}>
                                Home
                            </Link>
                            <Link to="/create" className={location.pathname === "/create" ? "active" : ""}>
                                Create Story
                            </Link>
                            <Link to="/about" onClick={handleAboutClick}
                                className={location.pathname === "/about" ? "active" : ""}>
                                About
                            </Link>

                            <button className="logout-button mobile-only" onClick={logout}>Logout</button>
                        </>
                    ) : (
                        <>
                            <Link to="/" onClick={handleHomeClick}
                                className={location.pathname === "/" ? "active" : ""}>
                                Home
                            </Link>
                            <Link to="/about" onClick={handleAboutClick}
                                className={location.pathname === "/about" ? "active" : ""}>
                                About
                            </Link>

                                <Link to="/login" className="login-button mobile-only">Login</Link>
                                <Link to="/signup" className="signup-button mobile-only">Sign Up</Link>
                        </>
                    )}
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
        </>
    )
}

export default NavBar