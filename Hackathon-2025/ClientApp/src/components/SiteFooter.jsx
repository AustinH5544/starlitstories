import instagramIcon from "../assets/social/instagram.png"
import facebookIcon from "../assets/social/facebook.png"
import xIcon from "../assets/social/x.png"
import "./SiteFooter.css"

const SiteFooter = () => {
    return (
        <footer className="footer">
            <div className="footer-content">
                <div className="footer-logo">
                    <span className="logo">Starlit Stories</span>
                    <p>Making bedtime magical, one story at a time</p>
                </div>

                <div className="footer-links">
                    <div className="footer-column">
                        <h4>Company</h4>
                        <a href="/about">About</a>
                    </div>

                    <div className="footer-column">
                        <h4>Resources</h4>
                        <a href="/blog">Blog</a>
                        <a href="/faq">FAQ</a>
                        <a href="/support">Support</a>
                    </div>

                    <div className="footer-links footer-meta-links">
                        <div className="footer-column">
                            <h4>Connect</h4>
                            <div className="social-links">
                                <a href="https://instagram.com/starlitstoriesapp" target="_blank" rel="noopener noreferrer" className="social-link" aria-label="Instagram">
                                    <img src={instagramIcon} alt="Instagram" className="social-icon" />
                                </a>
                                <a href="https://facebook.com/profile.php?id=61587218416327" target="_blank" rel="noopener noreferrer" className="social-link" aria-label="Facebook">
                                    <img src={facebookIcon} alt="Facebook" className="social-icon" />
                                </a>
                                <a href="https://x.com/starlitapp" target="_blank" rel="noopener noreferrer" className="social-link" aria-label="X">
                                    <img src={xIcon} alt="X" className="social-icon" />
                                </a>
                            </div>
                        </div>

                        <div className="footer-column contact-column">
                            <h4>Need help?</h4>
                            <a href="mailto:support@starlitstories.app" className="footer-email">
                                support@starlitstories.app
                            </a>
                        </div>
                    </div>
                </div>
            </div>

            <div className="footer-bottom">
                <p>{"\u00A9"} 2025 Starlit Stories. All rights reserved.</p>
            </div>
        </footer>
    )
}

export default SiteFooter
