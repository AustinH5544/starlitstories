import React, { useState } from 'react';
import NavBar from '../components/NavBar';
import './SignupPage.css';

const SignupPage = () => {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [confirm, setConfirm] = useState('');

    const handleSubmit = (e) => {
        e.preventDefault();
        if (password !== confirm) {
            alert("Passwords don't match");
            return;
        }
        // TODO: Send signup request to backend
        console.log({ email, password });
    };

    return (
        <>
            <NavBar />
            <div className="auth-container">
                <h2>Sign Up</h2>
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
                    <button type="submit">Create Account</button>
                </form>
            </div>
        </>
    );
};

export default SignupPage;
