#!/usr/bin/env node
/**
 * validate-codex-setup.js
 *
 * Checks Codex-facing setup files for references to missing skills and for
 * instructions that imply an unavailable native skill tool.
 */

'use strict';

const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..');
const SKILLS_DIR = path.join(ROOT, '.agents', 'skills');
const AGENTS_DIR = path.join(ROOT, 'agents');

const CODEX_FILES = [
  'AGENTS.md',
  ...fs.readdirSync(path.join(ROOT, 'commands'))
    .filter((file) => file.endsWith('.toml'))
    .map((file) => path.join('commands', file)),
];

const ALLOWED_NATIVE_TOOL_PHRASES = [
  'no native `skill` tool',
  'pretend that a platform-specific skill tool',
  'No synthetic `skill` tool call',
];

const IGNORED_CODE_TERMS = new Set([
  'kebab-case',
]);

function read(file) {
  return fs.readFileSync(path.join(ROOT, file), 'utf8');
}

function getKnownSkills() {
  return new Set(
    fs.readdirSync(SKILLS_DIR)
      .filter((entry) => fs.statSync(path.join(SKILLS_DIR, entry)).isDirectory())
      .sort()
  );
}

function getKnownAgents() {
  if (!fs.existsSync(AGENTS_DIR)) return new Set();

  return new Set(
    fs.readdirSync(AGENTS_DIR)
      .filter((entry) => entry.endsWith('.md'))
      .map((entry) => path.basename(entry, '.md'))
      .sort()
  );
}

function extractCodexRefs(content, knownSkills, knownAgents) {
  const refs = new Set();
  const patterns = [
    /`([a-z][a-z0-9-]+[a-z0-9])`/g,
    /\b(?:invoke|follow|use)\s+(?:the\s+)?`?([a-z][a-z0-9-]+[a-z0-9])`?\s+skill\b/gi,
    /\b([a-z][a-z0-9-]+[a-z0-9])\s+skill\b/gi,
  ];

  for (const pattern of patterns) {
    pattern.lastIndex = 0;
    let match;

    while ((match = pattern.exec(content)) !== null) {
      const value = match[1];
      if (IGNORED_CODE_TERMS.has(value)) continue;

      if (knownSkills.has(value) || knownAgents.has(value) || value.includes('-')) {
        refs.add(value);
      }
    }
  }

  return refs;
}

function hasUnavailableSkillToolInstruction(content) {
  const suspicious = [
    /must invoke (?:the )?`?skill`? tool/i,
    /use (?:the )?`?skill`? tool/i,
    /call (?:the )?`?skill`? tool/i,
  ];

  return suspicious.some((pattern) => {
    const match = content.match(pattern);
    if (!match) return false;

    const start = Math.max(0, match.index - 120);
    const end = Math.min(content.length, match.index + 180);
    const context = content.slice(start, end);

    return !ALLOWED_NATIVE_TOOL_PHRASES.some((phrase) => context.includes(phrase));
  });
}

function main() {
  const knownSkills = getKnownSkills();
  const knownAgents = getKnownAgents();
  let errors = 0;

  for (const file of CODEX_FILES) {
    if (!fs.existsSync(path.join(ROOT, file))) {
      console.log(`  x ${file}`);
      console.log('      ERROR: Missing Codex setup file');
      errors += 1;
      continue;
    }

    const content = read(file);
    const refs = extractCodexRefs(content, knownSkills, knownAgents);
    const missing = [...refs].filter((ref) => !knownSkills.has(ref) && !knownAgents.has(ref));

    if (missing.length > 0 || hasUnavailableSkillToolInstruction(content)) {
      console.log(`  x ${file}`);

      for (const ref of missing) {
        console.log(`      ERROR: References missing skill or agent \`${ref}\``);
        errors += 1;
      }

      if (hasUnavailableSkillToolInstruction(content)) {
        console.log('      ERROR: Implies Codex should use an unavailable native skill tool');
        errors += 1;
      }
    } else {
      console.log(`  OK ${file}`);
    }
  }

  const status = errors > 0 ? 'FAILED' : 'PASSED';
  console.log(`\nCodex setup validation ${status} - ${errors} error(s)`);

  if (errors > 0) process.exit(1);
}

try {
  main();
} catch (err) {
  console.error(`\nERROR: validate-codex-setup failed unexpectedly: ${err.message}`);
  process.exit(1);
}
