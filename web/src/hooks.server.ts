import type { Handle } from '@sveltejs/kit';
import { getTextDirection } from '#lib/paraglide/runtime';
import { paraglideMiddleware } from '#lib/paraglide/server';
import { languageTag } from '#lib/language-config';

const handleParaglide: Handle = ({ event, resolve }) =>
	paraglideMiddleware(event.request, ({ request, locale }) => {
		event.request = request;

		return resolve(event, {
			transformPageChunk: ({ html }) =>
				html
					.replace('%paraglide.lang%', languageTag(locale))
					.replace('%paraglide.dir%', getTextDirection(locale))
		});
	});

export const handle: Handle = handleParaglide;
