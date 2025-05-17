import { useNavigate } from 'react-router-dom'
import NavBar from '../components/NavBar'
import './LandingPage.css'

const LandingPage = () => {
    const navigate = useNavigate()

    return (
        <>
            <NavBar />
            <div className="hero">
                <div className="hero-text">
                    <h1>Welcome to your custom story generator</h1>
                    <p>Where your child's ideas can come to life</p>
                    <button onClick={() => navigate('/create')}>
                        Create your custom story
                    </button>
                </div>
            </div>
        </>
    )
}

export default LandingPage;