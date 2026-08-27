---
name: humanizer
description: >-
  Strips Wikipedia/chatbot AI-writing tells from prose so it sounds human.
  Use when drafting or editing Slack, email, docs, auto-replies, or pull
  request descriptions. Always apply before sending outward-facing copy
  and before opening or updating a PR.
---

# Humanizer

Remove signs of AI-generated writing. Based on [Wikipedia:Signs of AI writing](https://en.wikipedia.org/wiki/Wikipedia:Signs_of_AI_writing) (WikiProject AI Cleanup) and adapted from [blader/humanizer](https://github.com/blader/humanizer) (MIT). This file is a frozen snapshot; refresh from those sources rather than fetching at use time.

## When to use
- Before sending Slack, email, customer notes, docs, auto-replies, or pull request descriptions
- When another skill (e.g. Liam voice) produced a draft
- Embedded in larger jobs: run silently and return only the final text (no draft, no audit bullets, no summary)

## Process
1. Scan for the patterns below. **Clusters** matter more than one-offs.
2. Rewrite: keep every real fact; cut puffery and filler; match the intended voice.
3. Never invent facts, names, numbers, dates, quotes, or sources.
4. Self-audit: "What still makes this obviously AI?" Fix, then ship only the final text in embedded mode.

If the user (or Liam voice) provided a writing sample, match that sample's habits over generic scrubbing.

## Content patterns
1. **Puffed significance / legacy.** Cut: pivotal moment, testament to, evolving landscape, setting the stage, indelible mark, reflects broader, underscoring its importance, key turning point, focal point. State what happened.
2. **Notability dumps.** Don't list media outlets, trade publications, or "active social media presence" / "profiled in…" without a concrete point from a named source.
3. **Fake-depth -ing tails.** Cut trailing "highlighting…", "ensuring…", "reflecting…", "showcasing…", "fostering…", "cultivating…", "encompassing…".
4. **Promotional tone.** Avoid nestled, vibrant, breathtaking, groundbreaking, renowned, stunning, rich heritage, boasts, in the heart of, diverse array.
5. **Vague attributions.** No "experts argue", "industry reports", "observers note", "some critics argue" unless you name the source.
6. **Formulaic challenges/outlook.** Kill "Despite challenges… continues to thrive", "Future Outlook", and vague upbeat closers.

## Language patterns
7. **AI vocabulary.** Watch (and prefer plain alternatives): Additionally (esp. sentence-start), align with, bolstered, crucial, delve, emphasizing, enduring, enhance, fostering, garner, highlight (verb), interplay, intricate, key (adj), landscape (abstract), meticulous, pivotal, robust, showcase, tapestry, testament, underscore, valuable, vibrant. They often co-occur. **Grok-ish extras (as of 2026):** causal, empirical, correlate used as fancy padding; underscore still common.
8. **Copula avoidance.** Prefer is/are/has over serves as, stands as, marks, functions as, boasts, features, offers, refers to (when the subject is the thing, not the term).
9. **Negative parallelisms.** Rewrite "not just X, but Y", "it's not…, it's…", and Grok-leaning "X rather than Y" drama as the direct claim.
10. **Rule of three.** Don't force triads. Use the natural count.
11. **Elegant variation.** Repeat the same noun; don't cycle synonyms for the same thing.
12. **False ranges.** Avoid "from X to Y" when X and Y aren't on a real scale.

## Style patterns
13. **Em dashes.** Default for rewrites: none. Prefer periods, commas, parentheses, or a split sentence. Also catch spaced ` — ` and `--` used the same way. (If Liam voice / a sample uses them sparsely, match that sample instead of banning.) Detection note: as of mid-2026 some models (notably ChatGPT) suppress em dashes, so an em dash alone is a weak tell — treat it as evidence only in a cluster.
14. **Bold / emoji decoration.** No mechanical bold labels, no emoji section headers.
15. **Inline-header lists.** Convert `- **Label:** restatement` into prose unless the user asked for a list.
16. **Title Case headings.** Use sentence case.
17. **Curly quotes.** Prefer straight " and ' (curly alone is often Word/macOS, not AI).

## Communication artifacts
18. **Chatbot residue.** Cut: I hope this helps, Of course!, Certainly!, Great question!, You're absolutely right!, Would you like…, Let me know if you need anything else, Here's a…
19. **Cutoff / gap filler.** No "as of my last knowledge update", "while details are limited", "based on available information", "maintains a low profile", speculative "likely…".
20. **Sycophancy.** Answer; don't praise the asker.
21. **Placeholder / template residue.** Kill unfilled blanks: `[Your Name]`, `INSERT_…`, `PASTE_…_HERE`, `2025-XX-XX`, Mad Libs-style brackets left in the draft.

## Filler and rhetoric
22. Shorten: in order to → to; due to the fact that → because; at this point in time → now; it is important to note that → (delete).
23. Cut stacked hedges ("could potentially possibly").
24. No generic upbeat endings ("exciting times ahead", "major step in the right direction").
25. No signposting ("Let's dive in", "Here's what you need to know", "without further ado").
26. No manufactured punchline stacks (several clipped dramatic fragments in a row).
27. No persuasive-authority tropes ("the real question is", "at its core", "what really matters", "fundamentally") that restate an ordinary point with ceremony.
28. No fake-candid openers used as hooks ("Honestly?", "Look,", "Here's the thing", "Real talk") before a routine claim — just say the thing.
29. No aphorism formulas ("X is the Y of Z", "X becomes a trap", "the language/currency/architecture of…") when a concrete claim would do.

## Pasted-text tells (brief)
Usually Wikipedia/chatbot paste artifacts; worth stripping if they appear in a draft: Markdown heading hashes left in prose, `contentReference` / `oaicite` / `turn0search`, Gemini `[cite: N]` / `span_…` markers, Grok `grok_card` / citation-card JSON, DeepSeek `【…†…】` refs. Don't hunt these in normal Slack/email.

## False positives (do not over-edit)
- Clean grammar alone is not AI
- One "however", one short emphatic sentence, curly quotes from Word/macOS, or a lone em dash are fine
- Formal/academic words that aren't in the AI vocab list above are fine
- Preserve specific odd detail, mixed feelings, uneven rhythm, and first-person stance when the register calls for it
- Don't rewrite watched phrases inside quotations, titles, or proper names
- Flag **clusters** of tells, not isolated ones

## Slack / short-message mode
For DMs and chat: shorter is better. Fragments OK. Lead with the answer. No essay structure, no "Challenges and Future Outlook", no bold takeaways. Sound like a busy teammate, not a briefing doc.

## Pull request mode
Keep the template headings. Cut filler inside sections. Do not add "Summary", "Key takeaways", or a closer. For test-coverage PRs, prefer a compact case list over narrative.

## Reference
Wikipedia key insight: LLMs regress to the statistically average phrasing that fits the widest case. Humans keep the weird specific.
