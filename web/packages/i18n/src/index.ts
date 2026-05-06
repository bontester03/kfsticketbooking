import i18n from 'i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import { initReactI18next } from 'react-i18next';

import enCommon from './locales/en/common.json';
import arCommon from './locales/ar/common.json';

export type SupportedLocale = 'en' | 'ar';

export interface InitI18nOptions {
  defaultLocale?: SupportedLocale;
  app?: 'portal' | 'admin' | 'scanner';
}

let initialised = false;

export function initI18n({ defaultLocale = 'ar', app = 'portal' }: InitI18nOptions = {}) {
  if (initialised) return i18n;
  void i18n
    .use(LanguageDetector)
    .use(initReactI18next)
    .init({
      resources: {
        en: { common: enCommon },
        ar: { common: arCommon }
      },
      fallbackLng: defaultLocale,
      lng: defaultLocale,
      defaultNS: 'common',
      interpolation: { escapeValue: false },
      detection: {
        order: ['localStorage', 'navigator'],
        lookupLocalStorage: `kfs.${app}.lang`,
        caches: ['localStorage']
      }
    });

  i18n.on('languageChanged', (lng) => {
    document.documentElement.lang = lng;
    document.documentElement.dir = lng === 'ar' ? 'rtl' : 'ltr';
  });
  // Initial direction.
  document.documentElement.lang = i18n.language || defaultLocale;
  document.documentElement.dir = (i18n.language || defaultLocale) === 'ar' ? 'rtl' : 'ltr';

  initialised = true;
  return i18n;
}

export { useTranslation, Trans } from 'react-i18next';
export default i18n;
