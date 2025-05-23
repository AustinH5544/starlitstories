import React, { useState, useEffect } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import axios from 'axios';
import { useAuth } from '../context/AuthContext';
import './SignupPage.css';

const SignupComplete = () => {
    const [searchParams] = useSearchParams();
    const email = searchParams.get('email');
    const membership = searchParams.get('plan');
    const navigate = useNavigate();
    const { login } = useAuth();

    const [password, setPassword] = useState('');
    const [confirm, setConfirm] = useState('');
    const [status, setStatus] = useState('');

    const handleSubmit = async (e) => {
        e.preventDefault();
        if (password !== confirm) {
            setStatus("Passwords don't match.");
            return;
        }

        try {
            const response = await axios.post('http://localhost:5275/api/auth/signup', {
                email,
                password,
                membership,
            });

            login(response.data);
            navigate('/');
        } catch (err) {
            console.error(err);
            setStatus('Signup failed. Please try again.');
        }
    };

    if (!email || !membership) {
        return <p>Missing information in Stripe redirect URL.</p>;
    }

    return (
        <div className="auth-container">
            <h2>Welcome! Let’s finish setting up your account.</h2>
            <p>Email: <strong>{email}</strong></p>
            <p>Plan: <strong>{membership}</strong></p>
            <form onSubmit={handleSubmit} className="auth-form">
                <input
                    type="password"
                    placeholder="Create password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    required
                />
                <input
                    type="password"
                    placeholder="Confirm password"
                    value={confirm}
                    onChange={(e) => setConfirm(e.target.value)}
                    required
                />
                <button type="submit">Complete Signup</button>
                {status && <p className="auth-error">{status}</p>}
            </form>
        </div>
    );
};

export default SignupComplete;