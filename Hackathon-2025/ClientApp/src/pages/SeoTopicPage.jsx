import { Link } from "react-router-dom"
import { Helmet } from "react-helmet-async"
import ReactMarkdown from "react-markdown"
import SiteFooter from "../components/SiteFooter"
import seoTopics from "../content/seoTopics"
import "./SeoTopicPage.css"

const SeoTopicPage = ({ slug }) => {
    const topic = seoTopics.find((entry) => entry.slug === slug)

    if (!topic) {
        return null
    }

    const pageUrl = `https://starlitstories.app/blog/${topic.slug}`
    const webPageSchema = {
        "@context": "https://schema.org",
        "@type": "WebPage",
        "name": topic.title,
        "url": pageUrl,
        "description": topic.metaDescription,
    }

    const faqSchema = {
        "@context": "https://schema.org",
        "@type": "FAQPage",
        "mainEntity": topic.faqs.map((faq) => ({
            "@type": "Question",
            "name": faq.question,
            "acceptedAnswer": {
                "@type": "Answer",
                "text": faq.answer,
            },
        })),
    }

    return (
        <div className="seo-topic-page">
            <Helmet>
                <title>{topic.title}</title>
                <meta name="description" content={topic.metaDescription} />
                <link rel="canonical" href={pageUrl} />
                <meta property="og:title" content={topic.title} />
                <meta property="og:description" content={topic.metaDescription} />
                <meta property="og:type" content="website" />
                <meta property="og:url" content={pageUrl} />
                <meta property="og:image" content="https://starlitstories.app/og-image.png" />
                <meta property="og:site_name" content="Starlit Stories" />
                <meta name="twitter:card" content="summary_large_image" />
                <meta name="twitter:title" content={topic.title} />
                <meta name="twitter:description" content={topic.metaDescription} />
                <meta name="twitter:image" content="https://starlitstories.app/og-image.png" />
                <script type="application/ld+json">{JSON.stringify(webPageSchema)}</script>
                <script type="application/ld+json">{JSON.stringify(faqSchema)}</script>
            </Helmet>
            <div className="stars"></div>
            <div className="twinkling"></div>
            <div className="clouds"></div>

            <div className="seo-topic-shell">
                <Link to="/blog" className="seo-topic-back">← Back to Blog</Link>
                <article className="seo-topic-card">
                    <span className="seo-topic-kicker">Starlit Stories Guide</span>
                    <h1 className="seo-topic-title">{topic.heroTitle}</h1>
                    <p className="seo-topic-intro">{topic.intro}</p>

                    <div className="seo-topic-content">
                        {topic.sections.map((section) => (
                            <section key={section.heading}>
                                <h2>{section.heading}</h2>
                                <div className="seo-topic-markdown">
                                    <ReactMarkdown>{section.body}</ReactMarkdown>
                                </div>
                            </section>
                        ))}
                    </div>

                    <section className="seo-topic-faq">
                        <h2>Frequently asked questions</h2>
                        <div className="seo-topic-faq-list">
                            {topic.faqs.map((faq) => (
                                <div key={faq.question} className="seo-topic-faq-item">
                                    <h3>{faq.question}</h3>
                                    <p>{faq.answer}</p>
                                </div>
                            ))}
                        </div>
                    </section>

                    <section className="seo-topic-related">
                        <h2>Related reading</h2>
                        <div className="seo-topic-related-grid">
                            {topic.relatedLinks.map((link) => (
                                <Link key={link.href} to={link.href} className="seo-topic-related-link">
                                    {link.label}
                                </Link>
                            ))}
                        </div>
                    </section>

                    <div className="seo-topic-cta">
                        <Link to="/signup" className="seo-topic-button">Create your first story</Link>
                        <Link to="/faq" className="seo-topic-secondary">Read the FAQ</Link>
                    </div>
                </article>
            </div>

            <SiteFooter />
        </div>
    )
}

export default SeoTopicPage
