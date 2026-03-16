import { Link } from "react-router-dom"
import { Helmet } from "react-helmet-async"
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
            <div className="sprinkle"></div>
            <div className="sprinkle2"></div>
            <div className="sprinkle3"></div>
            <div className="blog-container">
                <div className="blog-header">
                    <h1>Starlit Stories Blog</h1>
                    <p>Tips, ideas, and inspiration for making bedtime magical.</p>
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
        </div>
    )
}

export default BlogPage
