import { renderToString } from 'react-dom/server'
import { StaticRouter } from 'react-router'
import { HelmetProvider } from 'react-helmet-async'
import { Routes, Route } from 'react-router-dom'
import { AuthContext } from './context/AuthContext'
import NavBar from './components/NavBar'
import LandingPage from './pages/LandingPage'
import AboutPage from './pages/AboutPage'
import FAQPage from './pages/FAQPage'
import LoginPage from './pages/LoginPage'
import SignupPage from './pages/SignupPage'
import SupportPage from './pages/SupportPage'
import BlogPage from './pages/BlogPage'
import BlogPostPage from './pages/BlogPostPage'
import SeoTopicPage from './pages/SeoTopicPage'

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
            <Route path="/blog/personalized-bedtime-storybooks" element={<SeoTopicPage slug="personalized-bedtime-storybooks" />} />
            <Route path="/blog/ai-story-generator-for-kids" element={<SeoTopicPage slug="ai-story-generator-for-kids" />} />
            <Route path="/blog/personalized-childrens-books" element={<SeoTopicPage slug="personalized-childrens-books" />} />
            <Route path="/blog/:slug" element={<BlogPostPage />} />
            <Route path="/personalized-bedtime-storybooks" element={<SeoTopicPage slug="personalized-bedtime-storybooks" />} />
            <Route path="/ai-story-generator-for-kids" element={<SeoTopicPage slug="ai-story-generator-for-kids" />} />
            <Route path="/personalized-childrens-books" element={<SeoTopicPage slug="personalized-childrens-books" />} />
          </Routes>
        </StaticRouter>
      </AuthContext.Provider>
    </HelmetProvider>
  )
  return { html }
}
