import React, { useCallback, useEffect } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormInput from 'Components/Form/FormInput';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import { inputTypes, kinds } from 'Helpers/Props';
import { InputChanged } from 'typings/inputs';
import {
  OnChildStateChange,
  SetChildSave,
} from 'typings/Settings/SettingsState';
import translate from 'Utilities/String/translate';
import { useManageMetadataSourceSettings } from './useMetadataSourceSettings';

interface ApiKeysProps {
  setChildSave: SetChildSave;
  onChildStateChange: OnChildStateChange;
}

function ApiKeys({ setChildSave, onChildStateChange }: ApiKeysProps) {
  const {
    isFetching,
    isFetched,
    isSaving,
    error,
    settings,
    hasSettings,
    hasPendingChanges,
    saveSettings,
    updateSetting,
  } = useManageMetadataSourceSettings();

  const handleInputChange = useCallback(
    ({ name, value }: InputChanged) => {
      // @ts-expect-error - InputChanged name/value are not typed as keyof MetadataSourceSettingsModel
      updateSetting(name, value);
    },
    [updateSetting]
  );

  useEffect(() => {
    setChildSave(saveSettings);
  }, [saveSettings, setChildSave]);

  useEffect(() => {
    onChildStateChange({
      isSaving,
      hasPendingChanges,
    });
  }, [hasPendingChanges, isSaving, onChildStateChange]);

  // Both keys sit on one page with one Save button, so they share a single settings
  // instance: pending changes are held per instance, and only one save is registered.
  const showForm = hasSettings && isFetched && !error;

  return (
    <>
      <FieldSet legend={translate('Tmdb')}>
        {isFetching ? <LoadingIndicator /> : null}

        {!isFetching && error ? (
          <Alert kind={kinds.DANGER}>
            {translate('MetadataSourceSettingsLoadError')}
          </Alert>
        ) : null}

        {showForm ? (
          <Form>
            <FormRow>
              <FormLabel>{translate('TmdbApiKey')}</FormLabel>

              <FormInputHelpText
                text={translate('TmdbApiKeyHelpText')}
                link="https://www.themoviedb.org/settings/api"
              />
              <FormInput
                type={inputTypes.PASSWORD}
                name="tmdbApiKey"
                onChange={handleInputChange}
                {...settings.tmdbApiKey}
              />
            </FormRow>
          </Form>
        ) : null}
      </FieldSet>

      {showForm ? (
        <FieldSet legend={translate('TheTvdb')}>
          <Form>
            <FormRow>
              <FormLabel>{translate('TvdbApiKey')}</FormLabel>

              <FormInputHelpText
                text={translate('TvdbApiKeyHelpText')}
                link="https://thetvdb.com/api-information"
              />
              <FormInput
                type={inputTypes.PASSWORD}
                name="tvdbApiKey"
                onChange={handleInputChange}
                {...settings.tvdbApiKey}
              />
            </FormRow>

            <FormRow>
              <FormLabel>{translate('TvdbPin')}</FormLabel>

              <FormInputHelpText text={translate('TvdbPinHelpText')} />
              <FormInput
                type={inputTypes.PASSWORD}
                name="tvdbPin"
                onChange={handleInputChange}
                {...settings.tvdbPin}
              />
            </FormRow>
          </Form>
        </FieldSet>
      ) : null}
    </>
  );
}

export default ApiKeys;
