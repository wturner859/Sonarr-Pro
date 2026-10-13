import React, { useCallback, useRef, useState } from 'react';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import SettingsPage from 'Settings/SettingsPage';
import {
  SaveCallback,
  SettingsStateChange,
} from 'typings/Settings/SettingsState';
import translate from 'Utilities/String/translate';
import ApiKeys from './ApiKeys';
import Sources from './Sources';

function MetadataSourceSettings() {
  const saveOptions = useRef<() => void>();

  const [isSaving, setIsSaving] = useState(false);
  const [hasPendingChanges, setHasPendingChanges] = useState(false);

  const handleSetChildSave = useCallback((saveCallback: SaveCallback) => {
    saveOptions.current = saveCallback;
  }, []);

  const handleChildStateChange = useCallback(
    ({ isSaving, hasPendingChanges }: SettingsStateChange) => {
      setIsSaving(isSaving);
      setHasPendingChanges(hasPendingChanges);
    },
    []
  );

  const handleSavePress = useCallback(() => {
    saveOptions.current?.();
  }, []);

  return (
    <SettingsPage
      title={translate('MetadataSourceSettings')}
      isSaving={isSaving}
      hasPendingChanges={hasPendingChanges}
      onSavePress={handleSavePress}
    >
      <PageContentBody>
        <PageHeading
          scope={translate('Settings')}
          title={translate('MetadataSource')}
        />
        <Sources />

        <ApiKeys
          setChildSave={handleSetChildSave}
          onChildStateChange={handleChildStateChange}
        />
      </PageContentBody>
    </SettingsPage>
  );
}

export default MetadataSourceSettings;
