import { useEffect, useState } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import api from "../api";
import { useAuth } from "../context/AuthContext";
import "./AdminPage.css";

const DEFAULT_PREVIEW_COUNT = 6;

const prettifyLabel = (value) =>
    String(value ?? "")
        .replace(/([a-z])([A-Z])/g, "$1 $2")
        .replace(/[_-]+/g, " ")
        .replace(/\s+/g, " ")
        .trim()
        .replace(/\b\w/g, (char) => char.toUpperCase());

const formatCharacterSummary = (character) => {
    if (!character) return "";

    const details = Object.entries(character.descriptionFields || {})
        .filter(([, value]) => String(value ?? "").trim())
        .slice(0, 4)
        .map(([key, value]) => `${prettifyLabel(key)}: ${value}`);

    const header = [character.name, character.role].filter(Boolean).join(" | ");
    return [header, ...details].filter(Boolean).join(" | ");
};

const formatDateTime = (value) => {
    if (!value) return "--";
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return "--";
    return date.toLocaleString(undefined, {
        year: "numeric",
        month: "short",
        day: "numeric",
        hour: "numeric",
        minute: "2-digit",
    });
};

const filterItems = (items, query, toSearchText) => {
    const trimmed = query.trim().toLowerCase();
    if (!trimmed) return items;
    return items.filter((item) => toSearchText(item).toLowerCase().includes(trimmed));
};

const AdminPage = () => {
    const navigate = useNavigate();
    const { user } = useAuth();
    const [dashboard, setDashboard] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");
    const [workingToken, setWorkingToken] = useState("");
    const [userQuery, setUserQuery] = useState("");
    const [storyQuery, setStoryQuery] = useState("");
    const [shareQuery, setShareQuery] = useState("");
    const [showAllUsers, setShowAllUsers] = useState(false);
    const [showAllStories, setShowAllStories] = useState(false);
    const [showAllShares, setShowAllShares] = useState(false);
    const [selectedUserId, setSelectedUserId] = useState(null);
    const [membershipDraft, setMembershipDraft] = useState("Free");
    const [bonusBooksDraft, setBonusBooksDraft] = useState("0");
    const [usageAdjustDraft, setUsageAdjustDraft] = useState("0");
    const [resetUsageDraft, setResetUsageDraft] = useState(false);
    const [savingUser, setSavingUser] = useState(false);

    useEffect(() => {
        if (!user?.isAdmin) return;

        let alive = true;

        const loadDashboard = async () => {
            try {
                setLoading(true);
                setError("");
                const { data } = await api.get("/admin/dashboard");
                if (alive) setDashboard(data);
            } catch (err) {
                if (!alive) return;
                const status = err?.response?.status;
                setError(status === 403 ? "This account is not allowed to open the admin area." : "Could not load admin data.");
            } finally {
                if (alive) setLoading(false);
            }
        };

        loadDashboard();
        return () => { alive = false; };
    }, [user?.isAdmin]);

    if (!user) return <Navigate to="/login" replace />;
    if (!user.isAdmin) return <Navigate to="/profile" replace />;

    const handleRevokeShare = async (token) => {
        try {
            setWorkingToken(token);
            await api.delete(`/admin/shares/${token}`);
            setDashboard((prev) => {
                if (!prev) return prev;

                const target = (prev.recentShares || []).find((share) => share.token === token);
                const wasActive = target?.status === "active";

                const nextShares = (prev.recentShares || []).map((share) =>
                    share.token === token
                        ? { ...share, revokedUtc: new Date().toISOString(), status: "revoked" }
                        : share
                );

                return {
                    ...prev,
                    recentShares: nextShares,
                    stats: {
                        ...prev.stats,
                        activeShares: wasActive ? Math.max(0, (prev.stats?.activeShares ?? 0) - 1) : (prev.stats?.activeShares ?? 0),
                        revokedShares: (prev.stats?.revokedShares ?? 0) + (target?.status === "revoked" ? 0 : 1),
                    },
                };
            });
        } catch (err) {
            console.error("Failed to revoke share", err);
            alert("Could not revoke that share link.");
        } finally {
            setWorkingToken("");
        }
    };

    const handleCopy = async (value) => {
        try {
            await navigator.clipboard.writeText(value);
        } catch (err) {
            console.error("Clipboard copy failed", err);
        }
    };

    const handleReadStory = async (storyId) => {
        try {
            const { data } = await api.get(`/admin/stories/${storyId}`);
            navigate("/view", { state: { story: data } });
        } catch (err) {
            console.error("Failed to load story for admin read", err);
            alert("Could not open that story.");
        }
    };

    const selectUser = (entry) => {
        setSelectedUserId(entry.id);
        setMembershipDraft(entry.membership || "Free");
        setBonusBooksDraft("0");
        setUsageAdjustDraft("0");
        setResetUsageDraft(false);
    };

    const handleUserAdminSave = async () => {
        if (!selectedUserId) return;

        try {
            setSavingUser(true);
            const { data } = await api.patch(`/admin/users/${selectedUserId}`, {
                membership: membershipDraft,
                addOnBalanceDelta: Number.parseInt(bonusBooksDraft || "0", 10) || 0,
                booksGeneratedDelta: Number.parseInt(usageAdjustDraft || "0", 10) || 0,
                resetPeriodUsage: resetUsageDraft,
            });

            setDashboard((prev) => {
                if (!prev) return prev;

                const nextUsers = (prev.recentUsers || []).map((entry) =>
                    entry.id === selectedUserId
                        ? {
                            ...entry,
                            membership: data.membership,
                            planStatus: data.planStatus,
                            booksGenerated: data.booksGenerated,
                            addOnBalance: data.addOnBalance,
                            currentPeriodEndUtc: data.currentPeriodEndUtc,
                            cancelAtUtc: data.cancelAtUtc,
                        }
                        : entry
                );

                return {
                    ...prev,
                    recentUsers: nextUsers,
                    stats: {
                        ...prev.stats,
                        paidUsers: nextUsers.filter((entry) => String(entry.membership).toLowerCase() !== "free").length,
                    },
                };
            });

            setBonusBooksDraft("0");
            setUsageAdjustDraft("0");
            setResetUsageDraft(false);
        } catch (err) {
            console.error("Failed to update user from admin", err);
            alert(err?.response?.data?.message || "Could not update that user.");
        } finally {
            setSavingUser(false);
        }
    };

    const stats = dashboard?.stats ?? {};
    const recentUsers = dashboard?.recentUsers ?? [];
    const recentStories = dashboard?.recentStories ?? [];
    const recentShares = dashboard?.recentShares ?? [];
    const filteredUsers = filterItems(
        recentUsers,
        userQuery,
        (entry) => `${entry.username} ${entry.email} ${entry.membership} ${entry.planStatus}`
    );
    const filteredStories = filterItems(
        recentStories,
        storyQuery,
        (entry) => `${entry.title} ${entry.ownerUsername} ${entry.ownerEmail} ${entry.id}`
    );
    const filteredShares = filterItems(
        recentShares,
        shareQuery,
        (entry) => `${entry.storyTitle} ${entry.ownerUsername} ${entry.ownerEmail} ${entry.token} ${entry.status}`
    );
    const visibleUsers = showAllUsers ? filteredUsers : filteredUsers.slice(0, DEFAULT_PREVIEW_COUNT);
    const visibleStories = showAllStories ? filteredStories : filteredStories.slice(0, DEFAULT_PREVIEW_COUNT);
    const visibleShares = showAllShares ? filteredShares : filteredShares.slice(0, DEFAULT_PREVIEW_COUNT);
    const userToggleLabel = showAllUsers ? "Show less" : `Show more (${Math.max(filteredUsers.length - visibleUsers.length, 0)} more)`;
    const storyToggleLabel = showAllStories ? "Show less" : `Show more (${Math.max(filteredStories.length - visibleStories.length, 0)} more)`;
    const shareToggleLabel = showAllShares ? "Show less" : `Show more (${Math.max(filteredShares.length - visibleShares.length, 0)} more)`;
    const selectedUser = recentUsers.find((entry) => entry.id === selectedUserId) ?? null;

    return (
        <div className="admin-page">
            <div className="admin-shell">
                <header className="admin-hero">
                    <div>
                        <p className="admin-kicker">Internal ops</p>
                        <h1>Admin console</h1>
                        <p className="admin-subtitle">
                            A lightweight control room for user health, story activity, and shared link cleanup.
                        </p>
                    </div>
                    <div className="admin-badge">
                        <span>{user.email}</span>
                    </div>
                </header>

                {loading ? (
                    <div className="admin-panel admin-empty">Loading admin data...</div>
                ) : error ? (
                    <div className="admin-panel admin-empty">{error}</div>
                ) : (
                    <>
                        <section className="admin-stats-grid">
                            <article className="admin-stat-card">
                                <span className="admin-stat-label">Users</span>
                                <strong>{stats.totalUsers ?? 0}</strong>
                                <p>{stats.verifiedUsers ?? 0} verified</p>
                            </article>
                            <article className="admin-stat-card">
                                <span className="admin-stat-label">Paid users</span>
                                <strong>{stats.paidUsers ?? 0}</strong>
                                <p>Subscriptions currently above free tier</p>
                            </article>
                            <article className="admin-stat-card">
                                <span className="admin-stat-label">Stories</span>
                                <strong>{stats.totalStories ?? 0}</strong>
                                <p>{stats.storiesLast7Days ?? 0} created in the last 7 days</p>
                            </article>
                            <article className="admin-stat-card">
                                <span className="admin-stat-label">Generating now</span>
                                <strong>{stats.generatingStories ?? 0}</strong>
                                <p>Stories still waiting on pages</p>
                            </article>
                            <article className="admin-stat-card">
                                <span className="admin-stat-label">Active shares</span>
                                <strong>{stats.activeShares ?? 0}</strong>
                                <p>{stats.totalShares ?? 0} total links issued</p>
                            </article>
                            <article className="admin-stat-card">
                                <span className="admin-stat-label">Revoked / expired</span>
                                <strong>{(stats.revokedShares ?? 0) + (stats.expiredShares ?? 0)}</strong>
                                <p>{stats.revokedShares ?? 0} revoked, {stats.expiredShares ?? 0} expired</p>
                            </article>
                        </section>

                        <section className="admin-panels">
                            <div className="admin-panel">
                                <div className="admin-panel-head">
                                    <h2>Recent users</h2>
                                    <span>{filteredUsers.length} matches</span>
                                </div>
                                <div className="admin-tools">
                                    <input
                                        className="admin-search"
                                        type="search"
                                        placeholder="Search users by name, email, plan..."
                                        value={userQuery}
                                        onChange={(e) => setUserQuery(e.target.value)}
                                    />
                                    {filteredUsers.length > DEFAULT_PREVIEW_COUNT && (
                                        <button className="admin-toggle-button" onClick={() => setShowAllUsers((v) => !v)}>
                                            {userToggleLabel}
                                        </button>
                                    )}
                                </div>
                                <div className={`admin-table-wrap ${showAllUsers ? "is-scrollable" : ""}`}>
                                    <table className="admin-table">
                                        <thead>
                                            <tr>
                                                <th>User</th>
                                                <th>Plan</th>
                                                <th>Status</th>
                                                <th>Usage</th>
                                                <th>Action</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {visibleUsers.map((entry) => (
                                                <tr key={entry.id}>
                                                    <td>
                                                        <div className="admin-cell-primary">{entry.username}</div>
                                                        <div className="admin-cell-secondary">{entry.email}</div>
                                                    </td>
                                                    <td>{entry.membership}</td>
                                                    <td>{entry.isEmailVerified ? entry.planStatus : "unverified"}</td>
                                                    <td>{entry.booksGenerated} books, {entry.addOnBalance} add-ons</td>
                                                    <td>
                                                        <button
                                                            className="admin-link-button"
                                                            onClick={() => selectUser(entry)}
                                                        >
                                                            Manage
                                                        </button>
                                                    </td>
                                                </tr>
                                            ))}
                                            {visibleUsers.length === 0 && (
                                                <tr>
                                                    <td colSpan="5" className="admin-empty-row">No users matched that search.</td>
                                                </tr>
                                            )}
                                        </tbody>
                                    </table>
                                </div>

                                {selectedUser && (
                                    <div className="admin-user-editor">
                                        <div className="admin-user-editor-head">
                                            <div>
                                                <h3>Manage {selectedUser.username}</h3>
                                                <p>{selectedUser.email}</p>
                                            </div>
                                            <button className="admin-toggle-button" onClick={() => setSelectedUserId(null)}>
                                                Close
                                            </button>
                                        </div>

                                        <div className="admin-user-editor-grid">
                                            <label className="admin-field">
                                                <span>Plan</span>
                                                <select value={membershipDraft} onChange={(e) => setMembershipDraft(e.target.value)}>
                                                    <option value="Free">Free</option>
                                                    <option value="Pro">Pro</option>
                                                    <option value="Premium">Premium</option>
                                                </select>
                                            </label>

                                            <label className="admin-field">
                                                <span>Add bonus books</span>
                                                <input
                                                    type="number"
                                                    value={bonusBooksDraft}
                                                    onChange={(e) => setBonusBooksDraft(e.target.value)}
                                                />
                                            </label>

                                            <label className="admin-field">
                                                <span>Adjust books used</span>
                                                <input
                                                    type="number"
                                                    value={usageAdjustDraft}
                                                    onChange={(e) => setUsageAdjustDraft(e.target.value)}
                                                />
                                            </label>
                                        </div>

                                        <label className="admin-checkbox">
                                            <input
                                                type="checkbox"
                                                checked={resetUsageDraft}
                                                onChange={(e) => setResetUsageDraft(e.target.checked)}
                                            />
                                            <span>Reset current period usage counters</span>
                                        </label>

                                        <p className="admin-helper">
                                            Current: {selectedUser.membership} plan, {selectedUser.booksGenerated} books used, {selectedUser.addOnBalance} bonus books available.
                                        </p>

                                        <div className="admin-user-editor-actions">
                                            <button className="admin-danger-button" disabled={savingUser} onClick={handleUserAdminSave}>
                                                {savingUser ? "Saving..." : "Save changes"}
                                            </button>
                                        </div>
                                    </div>
                                )}
                            </div>

                            <div className="admin-panel">
                                <div className="admin-panel-head">
                                    <h2>Recent stories</h2>
                                    <span>{filteredStories.length} matches</span>
                                </div>
                                <div className="admin-tools">
                                    <input
                                        className="admin-search"
                                        type="search"
                                        placeholder="Search stories by title, owner, or id..."
                                        value={storyQuery}
                                        onChange={(e) => setStoryQuery(e.target.value)}
                                    />
                                    {filteredStories.length > DEFAULT_PREVIEW_COUNT && (
                                        <button className="admin-toggle-button" onClick={() => setShowAllStories((v) => !v)}>
                                            {storyToggleLabel}
                                        </button>
                                    )}
                                </div>
                                <div className={`admin-table-wrap ${showAllStories ? "is-scrollable" : ""}`}>
                                    <table className="admin-table">
                                        <thead>
                                            <tr>
                                                <th>Story</th>
                                                <th>Owner</th>
                                                <th>State</th>
                                                <th>Created</th>
                                                <th>Action</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {visibleStories.map((entry) => (
                                                <tr key={entry.id}>
                                                    <td>
                                                        <div className="admin-cell-primary">{entry.title}</div>
                                                        <div className="admin-cell-secondary">#{entry.id} | {entry.pageCount} pages | {entry.shareCount} shares</div>
                                                        <div className="admin-story-request">
                                                            <div className="admin-story-request-row">
                                                                <span>Theme</span>
                                                                <strong>{entry.requestTheme || "--"}</strong>
                                                            </div>
                                                            <div className="admin-story-request-row">
                                                                <span>Lesson</span>
                                                                <strong>{entry.requestLessonLearned || "None"}</strong>
                                                            </div>
                                                            <div className="admin-story-request-row">
                                                                <span>Reading / Art</span>
                                                                <strong>{[entry.requestReadingLevel, entry.requestArtStyle].filter(Boolean).join(" / ") || "--"}</strong>
                                                            </div>
                                                            <div className="admin-story-request-row">
                                                                <span>Length</span>
                                                                <strong>{entry.requestStoryLength || "--"}</strong>
                                                            </div>
                                                            <div className="admin-story-request-row">
                                                                <span>Character</span>
                                                                <strong>{formatCharacterSummary(entry.requestCharacters?.[0]) || "--"}</strong>
                                                            </div>
                                                        </div>
                                                    </td>
                                                    <td>
                                                        <div className="admin-cell-primary">{entry.ownerUsername}</div>
                                                        <div className="admin-cell-secondary">{entry.ownerEmail}</div>
                                                    </td>
                                                    <td>{entry.isGenerating ? "Generating" : "Ready"}</td>
                                                    <td>{formatDateTime(entry.createdAt)}</td>
                                                    <td>
                                                        <button
                                                            className="admin-link-button"
                                                            onClick={() => handleReadStory(entry.id)}
                                                            disabled={entry.isGenerating}
                                                        >
                                                            {entry.isGenerating ? "Waiting" : "Read"}
                                                        </button>
                                                    </td>
                                                </tr>
                                            ))}
                                            {visibleStories.length === 0 && (
                                                <tr>
                                                    <td colSpan="5" className="admin-empty-row">No stories matched that search.</td>
                                                </tr>
                                            )}
                                        </tbody>
                                    </table>
                                </div>
                            </div>
                        </section>

                        <section className="admin-panel">
                            <div className="admin-panel-head">
                                <h2>Recent share links</h2>
                                <span>{filteredShares.length} matches</span>
                            </div>
                            <div className="admin-tools">
                                <input
                                    className="admin-search"
                                    type="search"
                                    placeholder="Search shares by story, owner, token, or status..."
                                    value={shareQuery}
                                    onChange={(e) => setShareQuery(e.target.value)}
                                />
                                {filteredShares.length > DEFAULT_PREVIEW_COUNT && (
                                    <button className="admin-toggle-button" onClick={() => setShowAllShares((v) => !v)}>
                                        {shareToggleLabel}
                                    </button>
                                )}
                            </div>
                            <div className={`admin-share-list ${showAllShares ? "is-scrollable" : ""}`}>
                                {visibleShares.map((share) => (
                                    <article key={share.token} className="admin-share-card">
                                        <div className="admin-share-main">
                                            <div className="admin-share-topline">
                                                <span className={`admin-status admin-status-${share.status}`}>{share.status}</span>
                                                <button className="admin-link-button" onClick={() => handleCopy(share.url)}>Copy link</button>
                                            </div>
                                            <h3>{share.storyTitle}</h3>
                                            <p>{share.ownerUsername} | {share.ownerEmail}</p>
                                            <p className="admin-share-meta">
                                                Created {formatDateTime(share.createdUtc)} | Expires {formatDateTime(share.expiresUtc)} | Revoked {formatDateTime(share.revokedUtc)}
                                            </p>
                                        </div>
                                        <div className="admin-share-actions">
                                            <code>{share.token}</code>
                                            <button
                                                className="admin-danger-button"
                                                onClick={() => handleRevokeShare(share.token)}
                                                disabled={share.status !== "active" || workingToken === share.token}
                                            >
                                                {workingToken === share.token ? "Revoking..." : share.status === "revoked" ? "Revoked" : share.status === "expired" ? "Expired" : "Revoke"}
                                            </button>
                                        </div>
                                    </article>
                                ))}
                                {visibleShares.length === 0 && (
                                    <div className="admin-empty-row admin-share-empty">No share links matched that search.</div>
                                )}
                            </div>
                        </section>
                    </>
                )}
            </div>
        </div>
    );
};

export default AdminPage;
