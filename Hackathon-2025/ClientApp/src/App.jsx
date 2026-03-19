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
import FAQPage from "./pages/FAQPage"
import SupportPage from "./pages/SupportPage"
import StoryCustomizePage from "./pages/StoryCustomizePage"
import BlogPage from "./pages/BlogPage"
import BlogPostPage from "./pages/BlogPostPage"
import SeoTopicPage from "./pages/SeoTopicPage"
import AdminPage from "./pages/AdminPage"
import ScrollToTop from "./components/ScrollToTop";

function App() {
    return (
        <AuthProvider>
            <BrowserRouter>
                <NavBar />
                <ScrollToTop />
                <Routes>
                    <Route path="/" element={<LandingPage />} />
                    <Route path="/create" element={<CreatePage />} />
                    <Route path="/about" element={<AboutPage />} />
                    <Route path="/login" element={<LoginPage />} />
                    <Route path="/signup" element={<SignupPage />} />
                    <Route path="/profile" element={<ProfilePage />} />
                    <Route path="/view" element={<StoryViewerPage />} />
                    <Route path="/s/:token" element={<StoryViewerPage mode="public" />} />
                    <Route path="/signup/complete" element={<SignupComplete />} />
                    <Route path="/upgrade" element={<UpgradePage />} />
                    <Route path="/forgot-password" element={<ForgotPasswordPage />} />
                    <Route path="/reset-password" element={<ResetPasswordPage />} />
                    <Route path="/verify-email" element={<VerifyEmailPage />} />
                    <Route path="/faq" element={<FAQPage />} />
                    <Route path="/support" element={<SupportPage />} />
                    <Route path="/customize" element={<StoryCustomizePage />} />
                    <Route path="/blog" element={<BlogPage />} />
                    <Route path="/blog/personalized-bedtime-storybooks" element={<SeoTopicPage slug="personalized-bedtime-storybooks" />} />
                    <Route path="/blog/ai-story-generator-for-kids" element={<SeoTopicPage slug="ai-story-generator-for-kids" />} />
                    <Route path="/blog/personalized-childrens-books" element={<SeoTopicPage slug="personalized-childrens-books" />} />
                    <Route path="/blog/:slug" element={<BlogPostPage />} />
                    <Route path="/personalized-bedtime-storybooks" element={<SeoTopicPage slug="personalized-bedtime-storybooks" />} />
                    <Route path="/ai-story-generator-for-kids" element={<SeoTopicPage slug="ai-story-generator-for-kids" />} />
                    <Route path="/personalized-childrens-books" element={<SeoTopicPage slug="personalized-childrens-books" />} />
                    <Route path="/admin" element={<AdminPage />} />
                </Routes>
            </BrowserRouter>
        </AuthProvider>
    )
}

export default App
