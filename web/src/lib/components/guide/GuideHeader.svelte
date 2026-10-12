<script lang="ts">
	import { env } from '$env/dynamic/public';
	import { page } from '$app/state';
	import * as m from '#lib/paraglide/messages.js';
	import * as Select from '#lib/components/ui/select/index.js';
	import { getLocale, locales, localizeHref } from '#lib/paraglide/runtime.js';
	import { REPOSITORY_URL } from '#lib';
	import type { Release } from './types';
	import Button from '../ui/button/button.svelte';
	let { latest, starCount }: { latest?: Release; starCount: number | null } = $props();
	let menuOpen = $state(false);
	const languageNames: Record<string, { short: string; name: string }> = {
		en: { short: 'EN', name: 'English' },
		vi: { short: 'VI', name: 'Tiếng Việt' },
		zh: { short: 'ZH', name: '简体中文' },
		kr: { short: 'KR', name: '한국어' },
		jp: { short: 'JP', name: '日本語' },
		de: { short: 'DE', name: 'Deutsch' }
	};
	let activeLocale = $derived(getLocale());
	const languages = $derived(
		locales.map((code) => ({
			code,
			...(languageNames[code] ?? { short: code.toUpperCase(), name: code })
		}))
	);
	let activeLanguage = $derived(
		languages.find((language) => language.code === activeLocale) ?? languages[0]
	);
	function changeLanguage(value: string) {
		const locale = locales.find((code) => code === value);
		if (!locale || locale === activeLocale) return;
		window.location.assign(
			localizeHref(page.url.pathname + page.url.search + page.url.hash, { locale })
		);
	}
	const links = $derived([
		{ href: '#features', label: m.nav_features() },
		{ href: '#install', label: m.nav_install() },
		{ href: '#releases', label: m.nav_releases() },
		{ href: '#help', label: m.nav_help() }
	]);
	function supportLink(value?: string) {
		try {
			const url = new URL(value || '');
			return url.protocol === 'https:' ? url.href : null;
		} catch {
			return null;
		}
	}
	const supportUrl = supportLink(env.PUBLIC_BUYME_URL);
</script>

<header class="site-header">
	<div class="header-inner">
		<a href="#main-content" class="brand" aria-label={m.brand_name()}>
			<span class="brand-mark" aria-hidden="true">5</span>
			<span><strong>{m.brand_name()}</strong><small>{m.brand_kicker()}</small></span>
		</a>
		<nav class="desktop-nav" aria-label={m.nav_primary()}>
			{#each links as link (link.href)}<a href={link.href}>{link.label}</a>{/each}
		</nav>
		<div class="header-actions">
			<Select.Root type="single" value={activeLocale} onValueChange={changeLanguage}>
				<Select.Trigger
					class="h-10 w-[5.5rem] rounded-lg border-border bg-secondary px-2.5 font-extrabold text-foreground"
					aria-label={m.language_label()}
				>
					<svg viewBox="0 0 24 24" aria-hidden="true"
						><circle
							cx="12"
							cy="12"
							r="9"
							fill="none"
							stroke="currentColor"
							stroke-width="1.6"
						/><path
							d="M3 12h18M12 3c5 6 5 12 0 18-5-6-5-12 0-18Z"
							fill="none"
							stroke="currentColor"
							stroke-width="1.6"
						/></svg
					>
					<span>{activeLanguage.short}</span>
				</Select.Trigger>
				<Select.Content
					align="end"
					class="w-52 rounded-xl border border-border bg-popover p-1.5 shadow-xl"
				>
					{#each languages as language (language.code)}
						<Select.Item value={language.code} label={language.name} class="rounded-lg py-2.5">
							<span class="min-w-0 flex-1">{language.name}</span><span
								class="text-xs font-bold text-muted-foreground">{language.short}</span
							>
						</Select.Item>
					{/each}
				</Select.Content>
			</Select.Root>
			<a
				href={REPOSITORY_URL}
				rel="external noreferrer"
				target="_blank"
				class="github-link"
				aria-label={starCount === null
					? m.source_link_label()
					: m.github_stars_label({ count: starCount })}
			>
				<svg viewBox="0 0 24 24" aria-hidden="true"
					><path
						fill="currentColor"
						d="M12 2a10 10 0 0 0-3.16 19.49c.5.09.68-.22.68-.48v-1.86c-2.78.61-3.37-1.18-3.37-1.18-.45-1.15-1.11-1.46-1.11-1.46-.91-.62.07-.61.07-.61 1 .07 1.53 1.03 1.53 1.03.89 1.53 2.34 1.09 2.91.83.09-.65.35-1.09.64-1.34-2.22-.25-4.56-1.11-4.56-4.94 0-1.09.39-1.98 1.03-2.68-.1-.25-.45-1.27.1-2.65 0 0 .84-.27 2.75 1.02A9.6 9.6 0 0 1 12 6.83c.85 0 1.71.12 2.51.34 1.91-1.29 2.75-1.02 2.75-1.02.55 1.38.2 2.4.1 2.65.64.7 1.03 1.59 1.03 2.68 0 3.84-2.34 4.69-4.57 4.94.36.31.68.92.68 1.85v2.74c0 .27.18.58.69.48A10 10 0 0 0 12 2Z"
					/></svg
				>
				{#if starCount !== null}<span
						>{new Intl.NumberFormat('en', { notation: 'compact' }).format(starCount)}</span
					>{/if}
			</a>
			<Button
				href={supportUrl}
				rel="external noreferrer"
				target="_blank"
				aria-label={m.support_label()}
			>
				Buy me a ☕
			</Button>
			<a class="game-button header-download" href={latest?.zipUrl || '#releases'}
				>{m.nav_download()} <span aria-hidden="true">↓</span></a
			>
			<button
				class="menu-toggle"
				onclick={() => (menuOpen = !menuOpen)}
				aria-expanded={menuOpen}
				aria-controls="mobile-nav"
				aria-label={m.nav_mobile()}
			>
				<svg viewBox="0 0 24 24" aria-hidden="true"
					><path
						d="M4 6h16M4 12h16M4 18h16"
						stroke="currentColor"
						stroke-width="2"
						stroke-linecap="round"
					/></svg
				>
			</button>
		</div>
	</div>
	{#if menuOpen}
		<nav id="mobile-nav" class="mobile-nav" aria-label={m.nav_primary()}>
			{#each links as link (link.href)}<a href={link.href} onclick={() => (menuOpen = false)}
					>{link.label}</a
				>{/each}
		</nav>
	{/if}
</header>

<style>
	.site-header {
		position: sticky;
		top: 0;
		z-index: 50;
		background: #c7d8e3f0;
		border-bottom: 1px solid #9ebdce;
		backdrop-filter: blur(16px);
	}
	.header-inner {
		max-width: 1280px;
		padding: 1rem 2rem;
		margin: auto;
		display: flex;
		align-items: center;
		gap: 2rem;
	}
	.brand {
		display: flex;
		align-items: center;
		gap: 0.75rem;
		flex-shrink: 1;
		min-width: 0;
	}
	.brand strong {
		display: block;
		font-size: 1.35rem;
		font-weight: 900;
		letter-spacing: -0.035em;
		white-space: nowrap;
		overflow: hidden;
		text-overflow: ellipsis;
	}
	.brand small {
		display: block;
		font-size: 0.65rem;
		color: var(--muted-foreground);
		font-weight: 750;
		letter-spacing: 0.07em;
		text-transform: uppercase;
	}
	.desktop-nav {
		display: flex;
		gap: 1.8rem;
		margin-left: auto;
	}
	nav a {
		font-size: 0.85rem;
		font-weight: 800;
		color: #456578;
	}
	nav a:hover {
		color: var(--primary);
	}
	.header-actions {
		display: flex;
		align-items: center;
		gap: 1rem;
		margin-left: auto;
	}
	.brand > span:last-child {
		min-width: 0;
	}
	.github-link {
		display: flex;
		align-items: center;
		gap: 0.3rem;
		font-size: 0.8rem;
		font-weight: 800;
	}
	.github-link svg,
	.menu-toggle svg {
		width: 22px;
		height: 22px;
	}
	.header-download {
		min-height: 40px;
		padding: 0.5rem 1rem;
		font-size: 0.85rem;
		border-radius: 0.7rem;
	}
	.menu-toggle {
		display: none;
		background: none;
		border: 1px solid var(--border);
		border-radius: 0.6rem;
		padding: 0.55rem;
		cursor: pointer;
	}
	.mobile-nav {
		display: flex;
		flex-wrap: wrap;
		justify-content: center;
		padding: 1rem;
		gap: 1.5rem;
		border-top: 1px solid var(--border);
	}
	@media (max-width: 1050px) {
		.desktop-nav {
			display: none;
		}
		.menu-toggle {
			display: block;
		}
		.header-inner {
			gap: 1rem;
		}
	}
	@media (max-width: 550px) {
		.header-inner {
			padding: 0.8rem 1rem;
		}
		.header-actions {
			gap: 0.7rem;
		}
		.header-download,
		.support-link,
		.github-link {
			display: none;
		}
		.brand small {
			font-size: 0.6rem;
		}
	}
	@media (max-width: 380px) {
		.header-inner {
			gap: 0.5rem;
		}
		.header-actions {
			gap: 0.45rem;
		}
		.brand {
			gap: 0.5rem;
		}
		.brand strong {
			font-size: 1.1rem;
		}
		.brand small {
			font-size: 0.5rem;
		}
		.brand :global(.brand-mark) {
			width: 36px;
			height: 38px;
			font-size: 1.3rem;
		}
		.github-link span {
			display: none;
		}
		.menu-toggle {
			padding: 0.45rem;
		}
	}
</style>
