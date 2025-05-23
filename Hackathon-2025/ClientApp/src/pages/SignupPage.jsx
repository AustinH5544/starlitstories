import React, { useState } from 'react';
import axios from 'axios';
import { useAuth } from '../context/AuthContext';
import { useNavigate } from 'react-router-dom';
//import { loadStripe } from '@stripe/stripe-js';
import './SignupPage.css';

const SignupPage = () => {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [confirm, setConfirm] = useState('');
    const [membership, setMembership] = useState('');

    const { login } = useAuth();
    const navigate = useNavigate();

    const handleSubmit = async (e) => {
        e.preventDefault();

        if (password !== confirm) {
            alert("Passwords don't match");
            return;
        }

        if (!membership) {
            alert("Please select a membership plan.");
            return;
        }

        if (membership === 'free') {
            // Free flow — signup immediately
            try {
                const response = await axios.post('http://localhost:5275/api/auth/signup', {
                    email,
                    password,
                    membership
                });
                login(response.data);
                navigate('/profile');
            } catch (err) {
                console.error(err);
                alert(err.response?.data || 'Signup failed');
            }
        } else {
            // Paid flow — redirect to Stripe
            try {
                const { data } = await axios.post('http://localhost:5275/api/payments/create-checkout-session', {
                    email,
                    membership
                });
                window.location.href = data.checkoutUrl;
            } catch (err) {
                console.error(err);
                alert('Error starting payment session');
            }
        }
    };

    return (
        <div className="auth-container">
            <h2>Create an Account</h2>
            <form onSubmit={handleSubmit} className="auth-form">
                <input
                    type="email"
                    placeholder="Email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    required
                />
                <input
                    type="password"
                    placeholder="Password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    required
                />
                <input
                    type="password"
                    placeholder="Confirm Password"
                    value={confirm}
                    onChange={(e) => setConfirm(e.target.value)}
                    required
                />

                <select
                    value={membership}
                    onChange={(e) => setMembership(e.target.value)}
                    required
                >
                    <option value="">Select Membership Plan</option>
                    <option value="free">Free — 1 book</option>
                    <option value="pro">Pro ($5/mo) — 10 books/month</option>
                    <option value="premium">Premium ($15/mo) — 50 books/month</option>
                </select>

                <button type="submit">Create Account</button>
            </form>
        </div>
    );
};

export default SignupPage;