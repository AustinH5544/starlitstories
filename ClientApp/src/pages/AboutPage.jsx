import React from 'react';
import './AboutPage.css'; // optional, for styling

const teamMembers = [
    {
        name: 'Tyler Woody',
        image: '/tyler.jpg',
        role: 'Full Stack Developer',
    },
    {
        name: 'Austin Harrison',
        image: '/austin.jpg',
        role: 'Full Stack Developer',
    },
];

const AboutPage = () => {
    return (
        <>
            <div className="about-container">
                <h2>About the AI Storybook Creator</h2>
                <p>
                    This project uses AI to help parents and kids create magical, personalized children's books. Stories are
                    written and illustrated with OpenAI and DALL·E, then packaged together into a visual experience.
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
