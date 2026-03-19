import { useParams, Link } from "react-router-dom"
import { Helmet } from "react-helmet-async"
import ReactMarkdown from "react-markdown"
import SiteFooter from "../components/SiteFooter"
import posts from "../content/blog/index.js"
import "./BlogPostPage.css"

const BlogPostPage = () => {
    const { slug } = useParams()
    const post = posts.find(p => p.slug === slug)

    if (!post) {
        return (
            <div className="blog-post-page">
                <div className="blog-post-container">
                    <div className="blog-post-not-found">
                        <h1>Post not found</h1>
                        <p>The article you're looking for doesn't exist.</p>
                        <Link to="/blog" className="blog-post-back">← Back to Blog</Link>
                    </div>
                </div>
            </div>
        )
    }

    const articleSchema = {
        "@context": "https://schema.org",
        "@type": "Article",
        "headline": post.title,
        "description": post.excerpt,
        "image": "https://starlitstories.app/og-image.png",
        "datePublished": post.date,
        "dateModified": post.date,
        "author": {
            "@type": "Organization",
            "name": "Starlit Stories",
        },
        "publisher": {
            "@type": "Organization",
            "name": "Starlit Stories",
            "logo": {
                "@type": "ImageObject",
                "url": "https://starlitstories.app/og-image.png",
            },
        },
        "mainEntityOfPage": `https://starlitstories.app/blog/${post.slug}`,
    }

    return (
        <div className="blog-post-page">
            <Helmet>
                <title>{`${post.title} | Starlit Stories`}</title>
                <meta name="description" content={post.excerpt} />
                <link rel="canonical" href={`https://starlitstories.app/blog/${post.slug}`} />
                <meta property="og:title" content={`${post.title} | Starlit Stories`} />
                <meta property="og:description" content={post.excerpt} />
                <meta property="og:type" content="article" />
                <meta property="og:url" content={`https://starlitstories.app/blog/${post.slug}`} />
                <meta property="og:image" content="https://starlitstories.app/og-image.png" />
                <meta property="og:site_name" content="Starlit Stories" />
                <meta name="twitter:card" content="summary_large_image" />
                <meta name="twitter:title" content={`${post.title} | Starlit Stories`} />
                <meta name="twitter:description" content={post.excerpt} />
                <meta name="twitter:image" content="https://starlitstories.app/og-image.png" />
                <script type="application/ld+json">{JSON.stringify(articleSchema)}</script>
            </Helmet>
            <div className="stars"></div>
            <div className="twinkling"></div>
            <div className="clouds"></div>
            <div className="blog-post-container">
                <Link to="/blog" className="blog-post-back">← Back to Blog</Link>
                <article className="blog-post-article">
                    <time className="blog-post-date">{post.date}</time>
                    <h1 className="blog-post-title">{post.title}</h1>
                    <div className="blog-post-content">
                        <ReactMarkdown>{post.content}</ReactMarkdown>
                    </div>
                </article>
            </div>
            <SiteFooter />
        </div>
    )
}

export default BlogPostPage
