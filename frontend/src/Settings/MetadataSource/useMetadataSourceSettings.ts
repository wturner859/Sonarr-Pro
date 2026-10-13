import { useManageSettings, useSettings } from 'Settings/useSettings';

export interface MetadataSourceSettingsModel {
  /**
   * Comes back masked once saved, never as the real key. Sending the mask back
   * leaves the stored key alone.
   */
  tmdbApiKey: string;
  tmdbApiKeyConfigured: boolean;

  /**
   * TheTVDB v4 key and, for a user-supported key, its PIN. Only needed for TheTVDB's
   * absolute and alternate orders. Masked once saved, like the TMDB key.
   */
  tvdbApiKey: string;
  tvdbApiKeyConfigured: boolean;
  tvdbPin: string;
  tvdbPinConfigured: boolean;
}

const PATH = '/settings/metadatasource';

export const useMetadataSourceSettings = () => {
  return useSettings<MetadataSourceSettingsModel>(PATH);
};

export const useManageMetadataSourceSettings = () => {
  return useManageSettings<MetadataSourceSettingsModel>(PATH);
};
