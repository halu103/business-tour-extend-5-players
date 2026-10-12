<script lang="ts">
	import * as m from '#lib/paraglide/messages.js';
	import { REPOSITORY_URL } from '#lib';
	import { getLocale } from '#lib/paraglide/runtime.js';
	import { languageTag } from '#lib/language-config';
	import type { Release } from './types';
	let { releases, releasesLive }: { releases: Release[]; releasesLive: boolean } = $props();
	let activeLocale = $derived(getLocale());
	function formatDate(value: string) {
		return new Intl.DateTimeFormat(languageTag(activeLocale), {
			year: 'numeric',
			month: 'short',
			day: 'numeric',
			timeZone: 'UTC'
		}).format(new Date(value));
	}
	function formatSize(bytes: number | null) {
		return bytes === null ? '' : (bytes / 1_000_000).toFixed(1) + ' MB';
	}
</script>

<section id="releases" class="section-shell" aria-labelledby="releases-title">
	<div class="release-heading">
		<div class="section-heading">
			<p class="section-kicker">{m.releases_kicker()}</p>
			<h2 id="releases-title">{m.releases_title()}</h2>
			<p>{m.releases_description()}</p>
		</div>
		<span class="mini-badge"
			><span class:live={releasesLive} class="status-dot" aria-hidden="true"></span>{releasesLive
				? m.release_live()
				: m.release_fallback()}</span
		>
	</div>
	{#if releases.length === 0}
		<div class="surface-card empty-release">
			<div>
				<h3>{m.release_empty_title()}</h3>
				<p class="muted-copy">{m.release_empty_body()}</p>
			</div>
			<a
				class="game-button secondary"
				href={REPOSITORY_URL + '/releases'}
				target="_blank"
				rel="external noreferrer">{m.footer_releases()} <span aria-hidden="true">↗</span></a
			>
		</div>
	{:else}
		<div class="release-list">
			{#each releases as release, index (release.tag)}
				<article
					class="surface-card release-card"
					class:latest-release={index === 0 && !release.prerelease}
				>
					<div class="release-top">
						<div>
							<div class="release-title">
								<h3>{release.version || release.tag}</h3>
								<span class="mini-badge" class:preview={release.prerelease}
									>{release.prerelease
										? m.release_prerelease()
										: index === 0
											? m.release_latest()
											: m.release_previous()}</span
								>
							</div>
							<p class="release-date">
								{m.release_published({ date: formatDate(release.publishedAt) })}
							</p>
							<p class="compatibility">
								{m.release_compatibility()}: {release.compatibility ||
									m.release_compatibility_unknown()}
							</p>
						</div>
						<a class="text-link" href={release.pageUrl} target="_blank" rel="external noreferrer"
							>{m.release_notes()} <span aria-hidden="true">↗</span></a
						>
					</div>
					<div class="release-actions">
						{#if release.zipUrl}<a class="game-button" href={release.zipUrl} rel="external"
								>{m.release_bundle()}
								<span class="file-size">{formatSize(release.zipSize)}</span><span aria-hidden="true"
									>↓</span
								></a
							>{/if}
						{#if release.installerUrl}<a
								class="game-button secondary"
								href={release.installerUrl}
								rel="external"
								>{m.release_installer()}
								<span class="file-size">{formatSize(release.installerSize)}</span></a
							>{/if}
						{#if release.checksumUrl}<a
								class="text-link checksum-link"
								href={release.checksumUrl}
								rel="external">{m.release_checksum()} <span aria-hidden="true">↗</span></a
							>{/if}
						{#if release.downloadCount !== null}<span class="download-count"
								>{new Intl.NumberFormat(languageTag(activeLocale)).format(release.downloadCount)}
								{m.release_downloads()}</span
							>{/if}
					</div>
				</article>
			{/each}
		</div>
	{/if}
</section>

<style>
	.release-heading {
		display: flex;
		align-items: flex-start;
		justify-content: space-between;
		gap: 2rem;
	}
	.release-heading .mini-badge {
		margin-top: 0.2rem;
		flex-shrink: 0;
	}
	.status-dot {
		width: 6px;
		height: 6px;
		border-radius: 50%;
		background: #be841e;
	}
	.status-dot.live {
		background: #608f26;
	}
	.release-list {
		display: grid;
		gap: 1.5rem;
	}
	.release-card {
		padding: 1.8rem 2rem;
	}
	.latest-release {
		border: 2px solid #7bbbd5;
	}
	.release-top {
		display: flex;
		align-items: flex-start;
		justify-content: space-between;
		gap: 1.5rem;
	}
	.release-title {
		display: flex;
		align-items: center;
		flex-wrap: wrap;
		gap: 0.8rem;
	}
	h3 {
		margin: 0;
		font-size: 1.5rem;
		font-weight: 900;
		letter-spacing: -0.025em;
	}
	.preview {
		background: #fff3ce;
		border-color: #e8cc73;
		color: #795b20;
	}
	.release-date,
	.compatibility {
		font-size: 0.8rem;
		color: var(--muted-foreground);
		margin-top: 0.6rem;
	}
	.release-actions {
		display: flex;
		flex-wrap: wrap;
		align-items: center;
		gap: 1rem;
		margin-top: 1.4rem;
		padding-top: 1.4rem;
		border-top: 1px solid #e3eff4;
	}
	.file-size {
		font-size: 0.75rem;
		font-weight: 650;
		opacity: 0.85;
	}
	.checksum-link {
		margin-left: 0.3rem;
		font-size: 0.8rem;
	}
	.download-count {
		margin-left: auto;
		font-size: 0.7rem;
		color: var(--muted-foreground);
	}
	.empty-release {
		display: flex;
		align-items: center;
		justify-content: space-between;
		gap: 2rem;
	}
	.empty-release h3 {
		font-size: 1.25rem;
	}
	.empty-release p {
		max-width: 620px;
		margin: 0.7rem 0 0;
		font-size: 0.95rem;
	}
	.empty-release .game-button {
		flex-shrink: 0;
	}
	@media (max-width: 750px) {
		.release-heading,
		.empty-release {
			flex-direction: column;
		}
		.release-heading {
			gap: 0;
		}
		.release-heading .mini-badge {
			margin: 0 0 1.5rem;
		}
		.release-top {
			flex-direction: column;
			gap: 1rem;
		}
		.release-card {
			padding: 1.3rem;
		}
		.download-count {
			width: 100%;
			margin: 0.3rem 0 0;
		}
	}
</style>
