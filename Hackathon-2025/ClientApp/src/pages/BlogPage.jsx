import { Link } from "react-router-dom"
import { Helmet } from "react-helmet-async"
import SiteFooter from "../components/SiteFooter"
import posts from "../content/blog/index.js"
import "./BlogPage.css"

const BlogPage = () => {
    return (
        <div className="blog-page">
            <Helmet>
                <title>Blog | Starlit Stories</title>
                <meta name="description" content="Tips, ideas, and inspiration for making bedtime magical with personalized children's storybooks." />
                <link rel="canonical" href="https://starlitstories.app/blog" />
                <meta property="og:title" content="Blog | Starlit Stories" />
                <meta property="og:description" content="Tips, ideas, and inspiration for making bedtime magical with personalized children's storybooks." />
                <meta property="og:type" content="website" />
                <meta property="og:url" content="https://starlitstories.app/blog" />
                <meta property="og:image" content="https://starlitstories.app/og-image.png" />
                <meta property="og:site_name" content="Starlit Stories" />
                <meta name="twitter:card" content="summary_large_image" />
                <meta name="twitter:title" content="Blog | Starlit Stories" />
                <meta name="twitter:description" content="Tips, ideas, and inspiration for making bedtime magical with personalized children's storybooks." />
                <meta name="twitter:image" content="https://starlitstories.app/og-image.png" />
            </Helmet>
            <div className="stars"></div>
            <div className="twinkling"></div>
            <div className="clouds"></div>
            <div className="blog-container">
                <div className="blog-header">
                    <h1>Starlit Stories Blog</h1>
                    <p>Tips, ideas, and inspiration for making bedtime magical.</p>
                </div>
                <div className="blog-list">
                    <article className="blog-card">
                        <time className="blog-card-date">Popular guide</time>
                        <h2 className="blog-card-title">
                            <Link to="/blog/personalized-bedtime-storybooks">Personalized Bedtime Storybooks</Link>
                        </h2>
                        <p className="blog-card-excerpt">Explore why personalized bedtime storybooks are such a strong fit for calm, memorable reading routines.</p>
                        <Link to="/blog/personalized-bedtime-storybooks" className="blog-card-link">Read the guide</Link>
                    </article>

                    <article className="blog-card">
                        <time className="blog-card-date">Popular guide</time>
                        <h2 className="blog-card-title">
                            <Link to="/blog/ai-story-generator-for-kids">AI Story Generator for Kids</Link>
                        </h2>
                        <p className="blog-card-excerpt">Learn how AI-powered storytelling can support reading habits while still feeling personal and child-centered.</p>
                        <Link to="/blog/ai-story-generator-for-kids" className="blog-card-link">Read the guide</Link>
                    </article>
                </div>
                <div className="blog-list">
                    {posts.map(post => (
                        <article key={post.slug} className="blog-card">
                            <time className="blog-card-date">{post.date}</time>
                            <h2 className="blog-card-title">
                                <Link to={`/blog/${post.slug}`}>{post.title}</Link>
                            </h2>
                            <p className="blog-card-excerpt">{post.excerpt}</p>
                            <Link to={`/blog/${post.slug}`} className="blog-card-link">Read more →</Link>
                        </article>
                    ))}
                </div>
            </div>
            <SiteFooter />
        </div>
    )
}

export default BlogPage
