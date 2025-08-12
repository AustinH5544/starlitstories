import { BrowserRouter, Routes, Route } from "react-router-dom"
import { AuthProvider } from "./context/AuthContext"

import NavBar from "./components/NavBar"
import LandingPage from "./pages/LandingPage"
import CreatePage from "./pages/CreatePage"
import AboutPage from "./pages/AboutPage"
import LoginPage from "./pages/LoginPage"
import SignupPage from "./pages/SignupPage"
import ProfilePage from "./pages/ProfilePage"
import StoryViewerPage from "./pages/StoryViewerPage"
import SignupComplete from "./pages/SignupComplete"
import UpgradePage from "./pages/UpgradePage"
import ForgotPasswordPage from "./pages/ForgotPasswordPage"
import ResetPasswordPage from "./pages/ResetPasswordPage"
import VerifyEmailPage from "./pages/VerifyEmailPage"

function App() {
    return (
        <AuthProvider>
            <BrowserRouter>
                <NavBar />
                <Routes>
                    <Route path="/" element={<LandingPage />} />
                    <Route path="/create" element={<CreatePage />} />
                    <Route path="/about" element={<AboutPage />} />
                    <Route path="/login" element={<LoginPage />} />
                    <Route path="/signup" element={<SignupPage />} />
                    <Route path="/profile" element={<ProfilePage />} />
                    <Route path="/view" element={<StoryViewerPage />} />
                    <Route path="/signup/complete" element={<SignupComplete />} />
                    <Route path="/upgrade" element={<UpgradePage />} />
                    <Route path="/forgot-password" element={<ForgotPasswordPage />} />
                    <Route path="/reset-password" element={<ResetPasswordPage />} />
                    <Route path="/verify-email" element={<VerifyEmailPage />} />
                </Routes>
            </BrowserRouter>
        </AuthProvider>
    )
}

export default App
