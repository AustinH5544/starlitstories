import React, { useState } from 'react';
import './SignupPage.css';

const SignupPage = () => {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [confirm, setConfirm] = useState('');
    const [membership, setMembership] = useState('');

    const handleSubmit = (e) => {
        e.preventDefault();

        if (password !== confirm) {
            alert("Passwords don't match");
            return;
        }

        if (!membership) {
            alert('Please select a membership plan.');
            return;
        }

        // TODO: Send signup info to backend
        console.log({ email, password, membership });
    };

    return (
        <>
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
                        <option value="free">Free — 1 book/month</option>
                        <option value="pro">Pro ($5/mo) — 10 books/month</option>
                        <option value="premium">Premium ($15/mo) — 50 books/month</option>
                    </select>

                    <button type="submit">Create Account</button>
                </form>
            </div>
        </>
    );
};

export default SignupPage;
