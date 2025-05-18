import './NavBar.css';
import { Link } from 'react-router-dom';

const NavBar = () => (
    <nav className="navbar">
        <div className="nav-left">
            <Link to="/">Home</Link>
            <Link to="/about">About</Link>
        </div>
        <div className="nav-center">
            <span className="logo">StoryGen</span>
        </div>
        <div className="nav-right">
            <a href="#">Sign Up</a>
            <a href="#">Login</a>
        </div>
    </nav>
);

export default NavBar;