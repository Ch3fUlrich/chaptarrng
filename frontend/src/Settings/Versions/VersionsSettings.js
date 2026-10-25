import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import SettingsToolbarConnector from 'Settings/SettingsToolbarConnector';
import { clearPendingChanges } from 'Store/Actions/baseActions';
import { fetchWantedVersions, saveWantedVersions, setWantedVersionsValue } from 'Store/Actions/settingsActions';
import createSettingsSectionSelector from 'Store/Selectors/createSettingsSectionSelector';
import translate from 'Utilities/String/translate';

const SECTION = 'wantedVersions';
const TAG_DELIMITERS = ['Tab', 'Enter', ','];

function createMapStateToProps() {
  return createSelector(
    createSettingsSectionSelector(SECTION),
    (sectionSettings) => sectionSettings
  );
}

const mapDispatchToProps = {
  dispatchFetch: fetchWantedVersions,
  dispatchSet: setWantedVersionsValue,
  dispatchSave: saveWantedVersions,
  dispatchClearPendingChanges: clearPendingChanges
};

class VersionsSettings extends Component {

  componentDidMount() {
    this.props.dispatchFetch();
  }

  componentWillUnmount() {
    this.props.dispatchClearPendingChanges({ section: 'settings.wantedVersions' });
  }

  onInputChange = ({ name, value }) => {
    this.props.dispatchSet({ name, value });
  };

  render() {
    const {
      isFetching,
      error,
      isSaving,
      settings,
      hasSettings,
      hasPendingChanges,
      dispatchSave
    } = this.props;

    return (
      <PageContent title={translate('Versions')}>
        <SettingsToolbarConnector
          isSaving={isSaving}
          hasPendingChanges={hasPendingChanges}
          onSavePress={dispatchSave}
        />

        <PageContentBody>
          {isFetching && <LoadingIndicator />}

          {!isFetching && error &&
            <Alert kind={kinds.DANGER}>
              {translate('UnableToLoadVersionsSettings')}
            </Alert>
          }

          {hasSettings && !isFetching && !error &&
            <Form>
              <Alert kind={kinds.INFO}>
                {translate('VersionsSettingsInfo')}
              </Alert>

              <FieldSet legend={translate('WantedLanguages')}>
                <FormGroup size={sizes.MEDIUM}>
                  <FormLabel>{translate('WantedLanguages')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.TEXT_TAG}
                    name="wantedVersionLanguages"
                    helpText={translate('WantedLanguagesHelpText')}
                    placeholder="deu"
                    kind={kinds.SUCCESS}
                    delimiters={TAG_DELIMITERS}
                    onChange={this.onInputChange}
                    {...settings.wantedVersionLanguages}
                  />
                </FormGroup>
              </FieldSet>

              <FieldSet legend={translate('WantedFormats')}>
                <FormGroup size={sizes.MEDIUM}>
                  <FormLabel>{translate('WantedEbookFormats')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.TEXT_TAG}
                    name="wantedVersionEbookFormats"
                    helpText={translate('WantedEbookFormatsHelpText')}
                    placeholder="epub"
                    delimiters={TAG_DELIMITERS}
                    onChange={this.onInputChange}
                    {...settings.wantedVersionEbookFormats}
                  />
                </FormGroup>

                <FormGroup size={sizes.MEDIUM}>
                  <FormLabel>{translate('WantedAudiobookFormats')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.TEXT_TAG}
                    name="wantedVersionAudiobookFormats"
                    helpText={translate('WantedAudiobookFormatsHelpText')}
                    placeholder="m4b"
                    delimiters={TAG_DELIMITERS}
                    onChange={this.onInputChange}
                    {...settings.wantedVersionAudiobookFormats}
                  />
                </FormGroup>
              </FieldSet>

              <FieldSet legend={translate('WantedVersionsAutomation')}>
                <FormGroup size={sizes.MEDIUM}>
                  <FormLabel>{translate('WantedVersionsFanOutOnMonitor')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.CHECK}
                    name="wantedVersionsFanOutOnMonitor"
                    helpText={translate('WantedVersionsFanOutOnMonitorHelpText')}
                    onChange={this.onInputChange}
                    {...settings.wantedVersionsFanOutOnMonitor}
                  />
                </FormGroup>
              </FieldSet>
            </Form>
          }
        </PageContentBody>
      </PageContent>
    );
  }
}

VersionsSettings.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  isSaving: PropTypes.bool.isRequired,
  hasPendingChanges: PropTypes.bool.isRequired,
  settings: PropTypes.object.isRequired,
  hasSettings: PropTypes.bool.isRequired,
  dispatchFetch: PropTypes.func.isRequired,
  dispatchSet: PropTypes.func.isRequired,
  dispatchSave: PropTypes.func.isRequired,
  dispatchClearPendingChanges: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(VersionsSettings);
