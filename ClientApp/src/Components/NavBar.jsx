import './NavBar.css';

const NavBar = () => (
    <nav className="navbar">
        <div className="nav-left">
            <a href="#">Home</a>
            <a href="#">About</a>
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