import PropTypes from 'prop-types';
import React from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

function DownloadClientOptions(props) {
  const {
    advancedSettings,
    isFetching,
    error,
    settings,
    hasSettings,
    onInputChange
  } = props;

  return (
    <div>
      {
        isFetching &&
          <LoadingIndicator />
      }

      {
        !isFetching && error &&
          <Alert kind={kinds.DANGER}>
            {translate('UnableToLoadDownloadClientOptions')}
          </Alert>
      }

      {
        hasSettings && !isFetching && !error && advancedSettings &&
          <div>
            <FieldSet legend={translate('CompletedDownloadHandling')}>
              <Form>
                <FormGroup
                  advancedSettings={advancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel>{translate('Enable')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.CHECK}
                    name="enableCompletedDownloadHandling"
                    helpText={translate('EnableCompletedDownloadHandlingHelpText')}
                    onChange={onInputChange}
                    {...settings.enableCompletedDownloadHandling}
                  />
                </FormGroup>
              </Form>
            </FieldSet>

            <FieldSet
              legend={translate('FailedDownloadHandling')}
            >
              <Form>
                <FormGroup
                  advancedSettings={advancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel>{translate('AutoRedownloadFailed')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.CHECK}
                    name="autoRedownloadFailed"
                    helpText={translate('AutoRedownloadFailedHelpText')}
                    onChange={onInputChange}
                    {...settings.autoRedownloadFailed}
                  />
                </FormGroup>

                {
                  settings.autoRedownloadFailed.value ?
                    <FormGroup
                      advancedSettings={advancedSettings}
                      isAdvanced={true}
                      size={sizes.MEDIUM}
                    >
                      <FormLabel>{translate('AutoRedownloadFailedFromInteractiveSearch')}</FormLabel>

                      <FormInputGroup
                        type={inputTypes.CHECK}
                        name="autoRedownloadFailedFromInteractiveSearch"
                        helpText={translate('AutoRedownloadFailedFromInteractiveSearchHelpText')}
                        onChange={onInputChange}
                        {...settings.autoRedownloadFailedFromInteractiveSearch}
                      />
                    </FormGroup> :
                    null
                }
              </Form>

              <Alert kind={kinds.INFO}>
                {translate('RemoveDownloadsAlert')}
              </Alert>
            </FieldSet>

            <FieldSet
              legend={translate('GrabBudget')}
            >
              <Form>
                <FormGroup
                  advancedSettings={advancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel>{translate('GrabBudgetEnabled')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.CHECK}
                    name="grabBudgetEnabled"
                    helpText={translate('GrabBudgetEnabledHelpText')}
                    onChange={onInputChange}
                    {...settings.grabBudgetEnabled}
                  />
                </FormGroup>

                <FormGroup
                  advancedSettings={advancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel>{translate('GrabBudgetMaxPerRun')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.NUMBER}
                    name="grabBudgetMaxPerRun"
                    min={0}
                    helpText={translate('GrabBudgetMaxPerRunHelpText')}
                    onChange={onInputChange}
                    {...settings.grabBudgetMaxPerRun}
                  />
                </FormGroup>

                <FormGroup
                  advancedSettings={advancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel>{translate('GrabBudgetMaxPerDay')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.NUMBER}
                    name="grabBudgetMaxPerDay"
                    min={0}
                    helpText={translate('GrabBudgetMaxPerDayHelpText')}
                    onChange={onInputChange}
                    {...settings.grabBudgetMaxPerDay}
                  />
                </FormGroup>

                <FormGroup
                  advancedSettings={advancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel>{translate('GrabBudgetMaxActiveQueue')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.NUMBER}
                    name="grabBudgetMaxActiveQueue"
                    min={0}
                    helpText={translate('GrabBudgetMaxActiveQueueHelpText')}
                    onChange={onInputChange}
                    {...settings.grabBudgetMaxActiveQueue}
                  />
                </FormGroup>

                <FormGroup
                  advancedSettings={advancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel>{translate('GrabBudgetMaxConsecutiveFailures')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.NUMBER}
                    name="grabBudgetMaxConsecutiveFailures"
                    min={0}
                    helpText={translate('GrabBudgetMaxConsecutiveFailuresHelpText')}
                    onChange={onInputChange}
                    {...settings.grabBudgetMaxConsecutiveFailures}
                  />
                </FormGroup>

                <FormGroup
                  advancedSettings={advancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel>{translate('GrabBudgetApplyToInteractive')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.CHECK}
                    name="grabBudgetApplyToInteractive"
                    helpText={translate('GrabBudgetApplyToInteractiveHelpText')}
                    onChange={onInputChange}
                    {...settings.grabBudgetApplyToInteractive}
                  />
                </FormGroup>

                <FormGroup
                  advancedSettings={advancedSettings}
                  isAdvanced={true}
                  size={sizes.MEDIUM}
                >
                  <FormLabel>{translate('GrabBudgetDryRun')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.CHECK}
                    name="grabBudgetDryRun"
                    helpText={translate('GrabBudgetDryRunHelpText')}
                    onChange={onInputChange}
                    {...settings.grabBudgetDryRun}
                  />
                </FormGroup>
              </Form>
            </FieldSet>
          </div>
      }
    </div>
  );
}

DownloadClientOptions.propTypes = {
  advancedSettings: PropTypes.bool.isRequired,
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  settings: PropTypes.object.isRequired,
  hasSettings: PropTypes.bool.isRequired,
  onInputChange: PropTypes.func.isRequired
};

export default DownloadClientOptions;
