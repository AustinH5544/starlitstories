import React from "react";

export default function PasswordChecklist({ requirements, labels, id = "password-reqs" }) {
    // `requirements` is { length: true/false, upper: true/false, ... }
    // `labels` is the output of requirementLabels()

    return (
        <ul id={id} className="pwd-reqs" aria-live="polite">
            {Object.keys(labels).map((key) => (
                <li key={key} className={requirements[key] ? "ok" : "pending"}>
                    {labels[key]}
                </li>
            ))}
        </ul>
    );
}
