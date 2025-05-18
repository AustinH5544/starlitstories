import { Routes, Route } from 'react-router-dom';
import LandingPage from './pages/LandingPage';
import CreatePage from './pages/CreatePage';
import AboutPage from './pages/AboutPage';

function App() {
    return (
        <Routes>
            <Route path="/" element={<LandingPage />} />
            <Route path="/create" element={<CreatePage />} />
            <Route path="/about" element={<AboutPage />} />
        </Routes>
    );
}

export default App;