import { renderToString } from 'react-dom/server'
import { StaticRouter } from 'react-router'
import { HelmetProvider } from 'react-helmet-async'
import { Routes, Route } from 'react-router-dom'
import { AuthContext } from './context/AuthContext'
import NavBar from './Components/NavBar'
import LandingPage from './pages/LandingPage'
import AboutPage from './pages/AboutPage'
import FAQPage from './pages/FAQPage'
import LoginPage from './pages/LoginPage'
import SignupPage from './pages/SignupPage'
import SupportPage from './pages/SupportPage'
import BlogPage from './pages/BlogPage'
import BlogPostPage from './pages/BlogPostPage'

const mockAuth = { user: null, setUser: () => {}, token: null, login: () => {}, logout: () => {} }

export async function render(url) {
  const html = renderToString(
    <HelmetProvider>
      <AuthContext.Provider value={mockAuth}>
        <StaticRouter location={url}>
          <NavBar />
          <Routes>
            <Route path="/" element={<LandingPage />} />
            <Route path="/about" element={<AboutPage />} />
            <Route path="/faq" element={<FAQPage />} />
            <Route path="/login" element={<LoginPage />} />
            <Route path="/signup" element={<SignupPage />} />
            <Route path="/support" element={<SupportPage />} />
            <Route path="/blog" element={<BlogPage />} />
            <Route path="/blog/:slug" element={<BlogPostPage />} />
          </Routes>
        </StaticRouter>
      </AuthContext.Provider>
    </HelmetProvider>
  )
  return { html }
}
