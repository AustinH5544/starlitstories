import React from "react";

export default function PasswordMatch({ confirmValue, isMatch }) {
    const cls =
        confirmValue.length === 0
            ? "match-indicator pending"
            : isMatch
                ? "match-indicator ok"
                : "match-indicator bad";

    return (
        <div className={cls} aria-live="polite">
            {confirmValue.length === 0
                ? "Re-enter your password to confirm"
                : isMatch
                    ? "✓ Passwords match"
                    : "✕ Passwords do not match"}
        </div>
    );
}
