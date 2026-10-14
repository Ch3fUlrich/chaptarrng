import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { inputTypes, kinds } from 'Helpers/Props';
import SettingsToolbarConnector from 'Settings/SettingsToolbarConnector';
import themes from 'Styles/Themes';
import titleCase from 'Utilities/String/titleCase';
import translate from 'Utilities/String/translate';

function getLocale() {
  return document.documentElement.lang || navigator.language || 'en';
}

function getDateParts(options) {
  const date = new Date(Date.UTC(2014, 2, 25, 17, 0));
  return new Intl.DateTimeFormat(getLocale(), {
    timeZone: 'UTC',
    ...options
  }).formatToParts(date).reduce((parts, part) => {
    parts[part.type] = part.value;
    return parts;
  }, {});
}

function getTime(hour, minute, hour12) {
  const date = new Date(Date.UTC(2014, 2, 25, hour, minute));
  return new Intl.DateTimeFormat(getLocale(), {
    timeZone: 'UTC',
    hour: 'numeric',
    minute: minute ? '2-digit' : undefined,
    hour12
  }).format(date);
}

export function getFirstDayOfWeekOptions() {
  return [
    { key: 0, value: translate('Sunday') },
    { key: 1, value: translate('Monday') }
  ];
}

export function getWeekColumnOptions() {
  const { weekday, month, day } = getDateParts({ weekday: 'short', month: 'numeric', day: 'numeric' });

  return [
    { key: 'ddd M/D', value: `${weekday} ${month}/${day}`, hint: 'ddd M/D' },
    { key: 'ddd MM/DD', value: `${weekday} ${month.padStart(2, '0')}/${day.padStart(2, '0')}`, hint: 'ddd MM/DD' },
    { key: 'ddd D/M', value: `${weekday} ${day}/${month}`, hint: 'ddd D/M' },
    { key: 'ddd DD/MM', value: `${weekday} ${day.padStart(2, '0')}/${month.padStart(2, '0')}`, hint: 'ddd DD/MM' }
  ];
}

function getShortDateFormatOptions() {
  const monthShort = getDateParts({ month: 'short' }).month;
  const numeric = getDateParts({ month: '2-digit', day: '2-digit', year: 'numeric' });

  return [
    { key: 'MMM D YYYY', value: `${monthShort} ${numeric.day} ${numeric.year}`, hint: 'MMM D YYYY' },
    { key: 'DD MMM YYYY', value: `${numeric.day} ${monthShort} ${numeric.year}`, hint: 'DD MMM YYYY' },
    { key: 'MM/D/YYYY', value: `${numeric.month}/${numeric.day}/${numeric.year}`, hint: 'MM/D/YYYY' },
    { key: 'MM/DD/YYYY', value: `${numeric.month}/${numeric.day}/${numeric.year}`, hint: 'MM/DD/YYYY' },
    { key: 'DD/MM/YYYY', value: `${numeric.day}/${numeric.month}/${numeric.year}`, hint: 'DD/MM/YYYY' },
    { key: 'YYYY-MM-DD', value: `${numeric.year}-${numeric.month}-${numeric.day}`, hint: 'YYYY-MM-DD' }
  ];
}

function getLongDateFormatOptions() {
  const first = getDateParts({ weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' });
  const second = getDateParts({ month: 'long', day: 'numeric', year: 'numeric' });

  return [
    { key: 'dddd, MMMM D YYYY', value: `${first.weekday}, ${first.month} ${first.day}, ${first.year}` },
    { key: 'dddd, D MMMM YYYY', value: `${first.weekday}, ${second.day} ${second.month}, ${second.year}` }
  ];
}

export function getTimeFormatOptions() {
  return [
    { key: 'h(:mm)a', value: `${getTime(17, 0, true)}/${getTime(17, 30, true)}`, hint: 'h(:mm)a' },
    { key: 'HH:mm', value: `${getTime(17, 0, false)}/${getTime(17, 30, false)}`, hint: 'HH:mm' }
  ];
}

class UISettings extends Component {

  //
  // Render

  render() {
    const {
      isFetching,
      error,
      settings,
      hasSettings,
      onInputChange,
      onSavePress,
      languages,
      ...otherProps
    } = this.props;

    const uiLanguages = languages.filter((item) => item.value !== 'Original');
    const themeOptions = Object.keys(themes)
      .map((theme) => ({ key: theme, value: theme === 'auto' ? translate('Automatic') : translate(titleCase(theme)) }));

    return (
      <PageContent title={translate('UISettings')}>
        <SettingsToolbarConnector
          {...otherProps}
          onSavePress={onSavePress}
        />

        <PageContentBody>
          {
            isFetching &&
              <LoadingIndicator />
          }

          {
            !isFetching && error &&
              <Alert kind={kinds.DANGER}>
                {translate('UnableToLoadUISettings')}
              </Alert>
          }

          {
            hasSettings && !isFetching && !error &&
              <Form
                id="uiSettings"
                {...otherProps}
              >
                <FieldSet legend={translate('Calendar')}>
                  <FormGroup>
                    <FormLabel>
                      {translate('FirstDayOfWeek')}
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.SELECT}
                      name="firstDayOfWeek"
                      values={getFirstDayOfWeekOptions()}
                      onChange={onInputChange}
                      {...settings.firstDayOfWeek}
                    />
                  </FormGroup>

                  <FormGroup>
                    <FormLabel>
                      {translate('WeekColumnHeader')}
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.SELECT}
                      name="calendarWeekColumnHeader"
                      values={getWeekColumnOptions()}
                      onChange={onInputChange}
                      helpText={translate('CalendarWeekColumnHeaderHelpText')}
                      {...settings.calendarWeekColumnHeader}
                    />
                  </FormGroup>
                </FieldSet>

                <FieldSet
                  legend={translate('Dates')}
                >
                  <FormGroup>
                    <FormLabel>
                      {translate('ShortDateFormat')}
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.SELECT}
                      name="shortDateFormat"
                      values={getShortDateFormatOptions()}
                      onChange={onInputChange}
                      {...settings.shortDateFormat}
                    />
                  </FormGroup>

                  <FormGroup>
                    <FormLabel>
                      {translate('LongDateFormat')}
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.SELECT}
                      name="longDateFormat"
                      values={getLongDateFormatOptions()}
                      onChange={onInputChange}
                      {...settings.longDateFormat}
                    />
                  </FormGroup>

                  <FormGroup>
                    <FormLabel>
                      {translate('TimeFormat')}
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.SELECT}
                      name="timeFormat"
                      values={getTimeFormatOptions()}
                      onChange={onInputChange}
                      {...settings.timeFormat}
                    />
                  </FormGroup>

                  <FormGroup>
                    <FormLabel>
                      {translate('ShowRelativeDates')}
                    </FormLabel>
                    <FormInputGroup
                      type={inputTypes.CHECK}
                      name="showRelativeDates"
                      helpText={translate('ShowRelativeDatesHelpText')}
                      onChange={onInputChange}
                      {...settings.showRelativeDates}
                    />
                  </FormGroup>
                </FieldSet>

                <FieldSet
                  legend={translate('Style')}
                >
                  <FormGroup>
                    <FormLabel>{translate('Theme')}</FormLabel>
                    <FormInputGroup
                      type={inputTypes.SELECT}
                      name="theme"
                      helpText={translate('ThemeHelpText')}
                      values={themeOptions}
                      onChange={onInputChange}
                      {...settings.theme}
                    />
                  </FormGroup>

                  <FormGroup>
                    <FormLabel>
                      {translate('EnableColorImpairedMode')}
                    </FormLabel>
                    <FormInputGroup
                      type={inputTypes.CHECK}
                      name="enableColorImpairedMode"
                      helpText={translate('EnableColorImpairedModeHelpText')}
                      onChange={onInputChange}
                      {...settings.enableColorImpairedMode}
                    />
                  </FormGroup>
                </FieldSet>

                <FieldSet legend={translate('Language')}>
                  <FormGroup>
                    <FormLabel>{translate('UILanguage')}</FormLabel>
                    <FormInputGroup
                      type={inputTypes.SELECT}
                      name="uiLanguage"
                      values={uiLanguages}
                      helpText={translate('UILanguageHelpText')}
                      helpTextWarning={translate('UILanguageHelpTextWarning')}
                      onChange={onInputChange}
                      {...settings.uiLanguage}
                      errors={
                        languages.some((language) => language.key === settings.uiLanguage.value) ?
                          settings.uiLanguage.errors :
                          [
                            ...settings.uiLanguage.errors,
                            { message: translate('InvalidUILanguage') }
                          ]}
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

UISettings.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  settings: PropTypes.object.isRequired,
  hasSettings: PropTypes.bool.isRequired,
  onSavePress: PropTypes.func.isRequired,
  languages: PropTypes.arrayOf(PropTypes.object).isRequired,
  onInputChange: PropTypes.func.isRequired
};

export default UISettings;
