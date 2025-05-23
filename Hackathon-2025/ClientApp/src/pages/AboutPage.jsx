import React from 'react';
import './AboutPage.css'; // optional, for styling

const teamMembers = [
    {
        name: 'Tyler Woody',
        image: '/tyler.jpg',
    },
    {
        name: 'Austin Harrison',
        image: '/austin.jpg',
    },
];

const AboutPage = () => {
    return (
        <>
            <div className="about-container">
                <h2>About the AI Storybook Creator</h2>
                <p>
                    This app empowers families to craft personalized, magical children's books using AI.
                    With a few clicks, parents and kids can generate a custom story—complete with vivid
                    illustrations—based on their chosen characters, themes, and imagination.
                </p>

                <h3>Why We Built This</h3>
                <p>
                    We believe every child deserves to see themselves as the hero of their own story.
                    Our goal was to make storytelling more accessible, creative, and fun by blending
                    technology with imagination.
                </p>

                <h3>How It Works</h3>
                <p>
                    Users enter character details (like name, age, animal species, or favorite colors) and a theme.
                    The backend uses OpenAI's GPT to generate a whimsical 8-paragraph narrative and DALL·E 3 to create
                    beautifully consistent illustrations. All of this comes together into a seamless storybook experience.
                </p>

                <h3>Technologies Used</h3>
                <ul style={{ textAlign: 'left', maxWidth: '600px', margin: '1rem auto' }}>
                    <li>React (Frontend)</li>
                    <li>ASP.NET Core Web API (Backend)</li>
                    <li>Entity Framework Core + SQLite</li>
                    <li>OpenAI GPT & DALL·E API</li>
                    <li>Azure (for hosting, storage, database)</li>
                </ul>

                <h3>Future Features</h3>
                <p>
                    We're planning to add story sharing, themed story packs, and
                    the ability to print physical copies of your AI-generated books!
                </p>

                <h3>Meet the Developers</h3>
                <div className="team-grid">
                    {teamMembers.map((member, idx) => (
                        <div className="team-member" key={idx}>
                            <img src={member.image} alt={member.name} className="team-photo" />
                            <h4>{member.name}</h4>
                            <p>{member.role}</p>
                        </div>
                    ))}
                </div>
            </div>
        </>
    );
};

export default AboutPage;
