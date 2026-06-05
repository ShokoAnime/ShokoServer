#! /bin/env node
import { dirname, join } from "node:path";
import { execSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import process from "node:process";

// https://git-scm.com/docs/pretty-formats/2.21.0

// Get the range or hash from the command line arguments
const RangeOrHash = process.argv[2] || "";

// Form the git log command
const GitLogCommandBase = `git log ${RangeOrHash}`;

const EndingMarkers = new Set([
    ".",
    ",",
    "!",
    "?",
]);

// Correct common type typos/synonyms to their canonical form.
const TypeAliases = {
    "feature": "feat",
    "refacor": "refactor",
    "mics": "misc",
};

// Slips where a scope was used as the type. Maps the bad type to the
// canonical [type, scope] pair (only applied when no scope was parsed).
const TypeToScope = {
    "docker": ["repo", "docker"],
};

// Correct common scope typos/synonyms to their canonical form.
const ScopeAliases = {
    "abstraction": "abstractions",
    "plugins": "plugin",
};

const Placeholders = {
    "H": "commit",
    "P": "parents",
    "T": "tree",
    "s": "subject",
    "b": "body",
    "an": "author_name",
    "ae": "author_email",
    "aI": "author_date",
    "cn": "committer_name",
    "ce": "committer_email",
    "cI": "committer_date",
};

const mappingUrl = import.meta.url.startsWith("file:")
    ? join(dirname(import.meta.url.slice(5)), "email-to-github.json")
    : null;
const emailToGithubMapping = mappingUrl && existsSync(mappingUrl)
    ? JSON.parse(readFileSync(mappingUrl, "utf-8"))
    : {};

const commitOrder = [];
const commits = {};

for (const [placeholder, name] of Object.entries(Placeholders)) {
    const gitCommand = `${GitLogCommandBase} --format="%H2>>>>> %${placeholder}"`;
    const output = execSync(gitCommand).toString();
    const lines = output.split(/\r\n|\r|\n/g);
    let commitId = "";

    for (const line of lines) {
        const match = line.match(/^([0-9a-f]{40})2>>>>> /);
        if (match) {
            commitId = match[1];
            if (!commits[commitId]) {
                commitOrder.push(commitId);
                commits[commitId] = {};
            }
            // Handle multiple parent hashes
            if (name === "parents") {
                commits[commitId][name] = line.substring(match[0].length).trim().split(" ");
            }
            else {
                commits[commitId][name] = line.substring(match[0].length).trimEnd();
            }
        }
        else if (commitId) {
            if (name === "parents") {
                const commits = line.trim().split(" ").filter(l => l);
                if (commits.length)
                    commits[commitId][name].push(...commits);
            }
            else {
                commits[commitId][name] += "\n" + line.trimEnd();
            }
        }
    }
}

// Add file-level changes to each commit
for (const commitId of commitOrder) {
    try {
        const fileStatusOutput = execSync(`git diff --name-status ${commitId}^ ${commitId}`).toString();
        const lineChangesOutput = execSync(`git diff --numstat ${commitId}^ ${commitId}`).toString();

        const files = [];
        const fileStatusLines = fileStatusOutput.split(/\r\n|\r|\n/g).filter(a => a);
        const lineChangesLines = lineChangesOutput.split(/\r\n|\r|\n/g).filter(a => a);

        for (const [index, line] of fileStatusLines.entries()) {
            const [rawStatus, path] = line.split(/\t/);
            const status = rawStatus === "M" ?
                "modified"
            : rawStatus === "A" ?
                "added"
            : rawStatus === "D" ?
                "deleted"
            : rawStatus === "R" ?
                "renamed"
            : "untracked";
            const lineChangeParts = lineChangesLines[index].split(/\t/);
            const addedLines = parseInt(lineChangeParts[0] || "0", 10);
            const removedLines = parseInt(lineChangeParts[1] || "0", 10);

            files.push({
                path,
                status,
                addedLines,
                removedLines,
            });
        }

        commits[commitId].files = files;
    }
    catch (error) {
        commits[commitId].files = [];
    }
}

// Trim trailing newlines from all values in the commits object
for (const commit of Object.values(commits)) {
    for (const key in commit) {
        if (typeof commit[key] === "string") {
            commit[key] = commit[key].trimEnd();
        }
    }
}

// Convert commits object to a list of values
const commitsList = commitOrder.reverse()
    .map((commitId) => commits[commitId])
    .map(({ commit, parents, tree, subject, body, author_name, author_email, author_date, committer_name, committer_email, committer_date, files }) => {
        // Conventional Commits: type(scope): subject  or  type: subject
        const conventionalMatch = subject.match(/^\s*(\w+)(?:\(([^)]*)\))?\s*:\s*(.*)$/);
        const parsedType = conventionalMatch ? conventionalMatch[1].toLowerCase() : null;
        const parsedScope = conventionalMatch ? (conventionalMatch[2] || null) : null;
        const parsedSubject = conventionalMatch ? conventionalMatch[3] : subject.trim();

        const type = parsedType
            ?? (subject.startsWith("Partially revert ") ? "revert"
            : parents.length > 1 ? "merge"
            : /^fix/i.test(subject) ? "fix"
            : "misc");

        return {
            commit,
            parents,
            tree,
            subject: parsedSubject,
            scope: parsedScope,
            type,
            body,
            author: {
                name: author_name,
                email: author_email,
                github: emailToGithubMapping[author_email] || null,
                date: new Date(author_date).toISOString(),
                timeZone: author_date.substring(19) === "Z" ? "+00:00" : author_date.substring(19),
            },
            committer: {
                name: committer_name,
                email: committer_email,
                github: emailToGithubMapping[committer_email] || null,
                date: new Date(committer_date).toISOString(),
                timeZone: committer_date.substring(19) === "Z" ? "+00:00" : committer_date.substring(19),
            },
            files,
        };
    })
    .map((commit) => {
        const prMatch = commit.subject.match(/\(#(\d+)\)/);
        const prNumber = prMatch ? parseInt(prMatch[1], 10) : null;

        // Normalize the type, then promote scope-as-type slips, then
        // normalize the resulting scope.
        let type = TypeAliases[commit.type] ?? commit.type;
        let scope = commit.scope;
        if (!scope && TypeToScope[type]) {
            [type, scope] = TypeToScope[type];
        }
        if (scope) {
            scope = ScopeAliases[scope] ?? scope;
        }

        return {
            ...commit,
            subject: commit.subject.replace(/\[(?:skip|no) *ci\]/ig, "").replace(/\(#\d+\)/g, "").trim().replace(/[\.:]+^/, ""),
            body: commit.body ? commit.body.replace(/\[(?:skip|no) *ci\]/ig, "").trimEnd() : commit.body,
            isSkipCI: /\[(?:skip|no) *ci\]/i.test(commit.subject) || Boolean(commit.body && /\[(?:skip|no) *ci\]/i.test(commit.body)),
            prNumber,
            type,
            scope,
        };
    })
    .map((commit) => ({
        ...commit,
        subject: ((subject) => {
            subject = (/[a-z]/.test(subject[0]) ? subject[0].toUpperCase() + subject.slice(1) : subject).trim();
            if (subject.length > 0 && EndingMarkers.has(subject[subject.length - 1]))
                subject = subject.slice(0, subject.length - 1);
            return subject;
        })(commit.subject),
    }))
    .map((commit, index) => ({
        ...commit,
        simple_type: ["misc", "refactor", "style", "perf", "test", "build", "ci", "docs", "revert"].includes(commit.type) ? "change" : commit.type === "chore" ? "repo" : commit.type,
        index,
    }));

process.stdout.write(JSON.stringify(commitsList, null, 2));
