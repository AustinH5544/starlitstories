import React, { useEffect, useState } from 'react';
import axios from '../api';
import './ProfilePage.css';
import { useAuth } from '../context/AuthContext';
import { useNavigate } from 'react-router-dom';

const ProfilePage = () => {
    const { user, logout } = useAuth();
    const [stories, setStories] = useState([]);
    const [loading, setLoading] = useState(true);
    const navigate = useNavigate();

    useEffect(() => {
        const fetchStories = async () => {
            try {
                const res = await axios.get(`/profile/${user.email}/stories`);
                setStories(res.data);
            } catch (err) {
                console.error('Error loading stories:', err);
            } finally {
                setLoading(false);
            }
        };

        if (user?.email) {
            fetchStories();
        }
    }, [user]);

    if (!user) {
        return <div className="profile-container"><h2>You are not logged in.</h2></div>;
    }

    return (
        <div className="profile-container">
            <h2>Welcome, {user.name || user.email}!</h2>
            <div className="profile-details">
                <p><strong>Email:</strong> {user.email}</p>
                <p><strong>Membership:</strong> {user.membership || 'Free'}</p>
            </div>

            <button onClick={logout} className="logout-button">Log Out</button>

            <h3>Your Saved Stories</h3>
            {loading ? (
                <p>Loading your stories...</p>
            ) : stories.length === 0 ? (
                <p>You haven’t saved any stories yet.</p>
            ) : (
                <div className="story-list">
                    {stories.map(story => (
                        <div
                            key={story.id}
                            className="story-card"
                            onClick={() => navigate('/view', { state: { story } })}
                        >
                            <img src={story.coverImageUrl} alt="Cover" />
                            <h4>{story.title}</h4>
                            <p className="date">{new Date(story.createdAt).toLocaleDateString()}</p>
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
};

export default ProfilePage;