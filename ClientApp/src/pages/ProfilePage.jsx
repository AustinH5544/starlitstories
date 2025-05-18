import React from 'react';
import './ProfilePage.css';
import { useAuth } from '../context/AuthContext';

const ProfilePage = () => {
    const { user, logout } = useAuth();

    if (user === undefined) return <p>Loading...</p>;

    if (!user) {
        return (
            <div className="profile-container">
                <h2>You are not logged in.</h2>
            </div>
        );
    }

    return (
        <div className="profile-container">
            <h2>Welcome, {user.email}!</h2>
            <div className="profile-details">
                <p><strong>Email:</strong> {user.email}</p>
                <p><strong>Membership:</strong> {user.membership || 'Free'}</p>
            </div>
            <button onClick={logout} className="logout-button">Log Out</button>
        </div>
    );
};

export default ProfilePage;
