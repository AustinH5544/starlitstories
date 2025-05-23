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

    return (
        <nav className={`navbar ${scrolled ? "scrolled" : ""}`}>
            <div className="nav-container">
                <div className="nav-left">
                    <Link to="/" className="logo-link">
                        <span className="logo">CozyPages</span>
                        <span className="logo-icon">✨</span>
                    </Link>
                </div>

                <div className={`nav-center ${mobileMenuOpen ? "mobile-open" : ""}`}>
                    <Link to="/" className={location.pathname === "/" ? "active" : ""}>
                        Home
                    </Link>
                    <Link to="/create" className={location.pathname === "/create" ? "active" : ""}>
                        Create Story
                    </Link>
                    <Link to="/about" className={location.pathname === "/about" ? "active" : ""}>
                        About
                    </Link>
                    <a href="/#how-it-works" className="nav-link">
                        How It Works
                    </a>
                </div>

                <div className={`nav-right ${mobileMenuOpen ? "mobile-open" : ""}`}>
                    {!user ? (
                        <>
                            <Link to="/login" className="login-button">
                                Log In
                            </Link>
                            <Link to="/signup" className="signup-button">
                                Sign Up
                            </Link>
                        </>
                    ) : (
                        <div className="user-menu">
                            <Link to="/profile" className="profile-link">
                                <div className="user-avatar">{user.displayName ? user.displayName.charAt(0).toUpperCase() : "U"}</div>
                                <span className="user-name">{user.displayName || "My Account"}</span>
                            </Link>
                            <button className="logout-button" onClick={logout}>
                                Log Out
                            </button>
                        </div>
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