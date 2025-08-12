import { useEffect, useState } from "react";
import axios from "../api";

const useUserProfile = () =>
{
    const [profile, setProfile] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);

    useEffect(() =>
    {
        const fetchProfile = async () =>
        {
            try
            {
                const res = await axios.get("/profile/me");
                setProfile(res.data);
            }
            catch (err)
            {
                console.error("Error fetching user profile:", err);
                setError(err.response?.data || "Something went wrong.");
            }
            finally
            {
                setLoading(false);
            }
        };

        fetchProfile();
    }, []);

    return { profile, loading, error };
};

export default useUserProfile;