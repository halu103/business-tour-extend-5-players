<script lang="ts">
	import GuideFooter from '#lib/components/guide/GuideFooter.svelte';
	import GuideHeader from '#lib/components/guide/GuideHeader.svelte';
	import HelpSection from '#lib/components/guide/HelpSection.svelte';
	import HeroSection from '#lib/components/guide/HeroSection.svelte';
	import FeaturesSection from '#lib/components/guide/FeaturesSection.svelte';
	import InstallSection from '#lib/components/guide/InstallSection.svelte';
	import ReleasesSection from '#lib/components/guide/ReleasesSection.svelte';
	import ScopeSection from '#lib/components/guide/ScopeSection.svelte';
	import VerifySection from '#lib/components/guide/VerifySection.svelte';
	import * as m from '#lib/paraglide/messages.js';
	import { REPOSITORY_URL } from '#lib';
	import { getLocale, locales, localizeHref, baseLocale } from '#lib/paraglide/runtime.js';
	import { languageTag, openGraphLocale } from '#lib/language-config';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
	let latest = $derived(data.releases[0]);
	let activeLocale = $derived(getLocale());
	let canonicalUrl = $derived(
		data.seo.canonicalUrl ? localizeHref(data.seo.canonicalUrl, { locale: activeLocale }) : null
	);
	const structuredData = $derived(
		JSON.stringify({
			'@context': 'https://schema.org',
			'@type': 'SoftwareApplication',
			name: 'Business Tour Five Realms',
			applicationCategory: 'GameApplication',
			operatingSystem: 'Windows x64',
			description: m.meta_description(),
			...(canonicalUrl ? { url: canonicalUrl } : {}),
			...(latest?.version ? { softwareVersion: latest.version } : {}),
			sameAs: REPOSITORY_URL
		}).replace(/</g, '\\u003c')
	);
</script>

<svelte:head>
	<title>{m.meta_title()}</title>
	<meta name="description" content={m.meta_description()} />
	<meta name="theme-color" content="#89b3cb" />
	<meta name="robots" content="index, follow" />
	<meta property="og:type" content="website" />
	<meta property="og:site_name" content="Five Realms" />
	<meta property="og:title" content={m.meta_title()} />
	<meta property="og:description" content={m.meta_description()} />
	<meta property="og:locale" content={openGraphLocale(activeLocale)} />
	<meta name="twitter:card" content={data.seo.imageUrl ? 'summary_large_image' : 'summary'} />
	<meta name="twitter:title" content={m.meta_title()} />
	<meta name="twitter:description" content={m.meta_description()} />
	{#if canonicalUrl && data.seo.canonicalUrl}
		<link rel="canonical" href={canonicalUrl} />
		<meta property="og:url" content={canonicalUrl} />
		{#each locales as locale}
			<link
				rel="alternate"
				hreflang={languageTag(locale)}
				href={localizeHref(data.seo.canonicalUrl, { locale })}
			/>
		{/each}
		<link
			rel="alternate"
			hreflang="x-default"
			href={localizeHref(data.seo.canonicalUrl, { locale: baseLocale })}
		/>
	{/if}
	{#if data.seo.imageUrl}
		<meta property="og:image" content={data.seo.imageUrl} />
		<meta property="og:image:alt" content={m.board_preview_label()} />
		<meta name="twitter:image" content={data.seo.imageUrl} />
	{/if}
	{@html '<script type="application/ld+json">' + structuredData + '</script>'}
</svelte:head>

<a href="#main-content" class="skip-link">{m.skip_to_content()}</a>
<GuideHeader {latest} starCount={data.starCount} />
<main id="main-content">
	<HeroSection {latest} />
	<FeaturesSection />
	<ScopeSection />
	<InstallSection {latest} />
	<VerifySection />
	<ReleasesSection releases={data.releases} releasesLive={data.releasesLive} />
	<HelpSection {latest} />
</main>
<GuideFooter />

<style>
	.skip-link {
		position: fixed;
		top: -100px;
		left: 1rem;
		z-index: 100;
		padding: 0.8rem 1rem;
		border-radius: 0.6rem;
		background: #fff;
		color: var(--primary);
		font-weight: 800;
		box-shadow: 0 3px 16px #13384d20;
	}
	.skip-link:focus {
		top: 1rem;
	}
</style>
