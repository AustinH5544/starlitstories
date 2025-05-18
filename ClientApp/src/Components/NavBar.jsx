import './NavBar.css';
import { Link } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

const NavBar = () => {
    const { user } = useAuth();

    return (
        <nav className="navbar">
            <div className="nav-left">
                <Link to="/">Home</Link>
                <Link to="/about">About</Link>
            </div>

            <div className="nav-center">
                <span className="logo">StoryGen</span>
            </div>

            <div className="nav-right">
                {!user ? (
                    <>
                        <Link to="/signup">Sign Up</Link>
                        <Link to="/login">Login</Link>
                    </>
                ) : (
                    <Link to="/profile">Profile</Link>
                )}
            </div>
        </nav>
    );
};

export default NavBar;