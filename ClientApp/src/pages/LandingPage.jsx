import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import './LandingPage.css';

const LandingPage = () => {
    const navigate = useNavigate();
    const { user } = useAuth();
    const [showWarning, setShowWarning] = useState(false);

    const handleCreateClick = () => {
        if (!user) {
            setShowWarning(true);
            return;
        }

        navigate('/create');
    };

    return (
        <div className="hero">
            <div className="hero-text">
                <h1>Welcome to your custom story generator</h1>
                <p>Where your child's ideas can come to life</p>

                <button onClick={handleCreateClick}>
                    Create your custom story
                </button>

                {showWarning && (
                    <p className="warning-message">You must be logged in to create a custom story.</p>
                )}
            </div>
        </div>
    );
};

export default LandingPage;
