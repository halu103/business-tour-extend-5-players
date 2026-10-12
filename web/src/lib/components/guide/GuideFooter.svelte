<script lang="ts">
	import { Download04Icon, Github01Icon } from '@hugeicons/core-free-icons';
	import { HugeiconsIcon } from '@hugeicons/svelte';
	import * as m from '#lib/paraglide/messages.js';
	import { REPOSITORY_URL } from '#lib';

	const allReleasesUrl = `${REPOSITORY_URL}/releases`;
</script>

<footer class="guide-footer">
	<div class="section-shell footer-inner">
		<div class="footer-copy">
			<p class="footer-notice">{m.footer_notice()}</p>
			<p class="muted-copy">{m.footer_disclaimer()}</p>
		</div>
		<nav class="footer-links" aria-label={m.footer_github()}>
			<a href={REPOSITORY_URL} target="_blank" rel="external noreferrer"
				><HugeiconsIcon
					icon={Github01Icon}
					strokeWidth={2}
					aria-hidden="true"
				/>{m.footer_github()}</a
			>
			<a href={allReleasesUrl} target="_blank" rel="external noreferrer"
				><HugeiconsIcon
					icon={Download04Icon}
					strokeWidth={2}
					aria-hidden="true"
				/>{m.footer_releases()}</a
			>
		</nav>
	</div>
</footer>

<style>
	.guide-footer {
		border-top: 1px solid var(--border);
		background: var(--card);
	}
	.footer-inner {
		display: flex;
		justify-content: space-between;
		align-items: center;
		gap: 2rem;
		padding-block: 2.3rem;
	}
	.footer-copy {
		max-width: 43rem;
	}
	.footer-notice {
		font-weight: 800;
		margin: 0 0 0.5rem;
	}
	.footer-copy .muted-copy {
		margin: 0;
		font-size: 0.85rem;
		line-height: 1.7;
	}
	.footer-links {
		display: flex;
		flex-wrap: wrap;
		align-items: center;
		gap: 1.1rem;
		flex: none;
	}
	.footer-links a {
		display: inline-flex;
		align-items: center;
		gap: 0.45rem;
		color: var(--primary);
		font-size: 0.85rem;
		font-weight: 750;
		text-decoration: none;
		padding-block: 0.5rem;
	}
	.footer-links a:hover {
		text-decoration: underline;
		text-underline-offset: 0.25rem;
	}
	.footer-links :global(svg) {
		width: 1.1rem;
		height: 1.1rem;
	}
	@media (max-width: 850px) {
		.footer-inner {
			align-items: flex-start;
			flex-direction: column;
			gap: 1rem;
		}
	}
</style>
