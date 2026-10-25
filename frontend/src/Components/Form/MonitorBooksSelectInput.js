import PropTypes from 'prop-types';
import React from 'react';
import monitorOptions from 'Utilities/Author/monitorOptions';
import { ALL_AUDIOBOOKS, ALL_EBOOKS } from 'Utilities/Author/monitorScope';
import translate from 'Utilities/String/translate';
import SelectInput from './SelectInput';

function MonitorBooksSelectInput(props) {
  const {
    includeNoChange,
    includeMixed,
    includeSpecificBook,
    includeMediaScopes,
    ...otherProps
  } = props;

  const values = [...monitorOptions];

  if (includeMediaScopes) {
    const allIndex = values.findIndex((v) => v.key === 'all');
    values.splice(allIndex + 1, 0,
      { key: ALL_EBOOKS, value: translate('MonitorAllEbooks') },
      { key: ALL_AUDIOBOOKS, value: translate('MonitorAllAudiobooks') });
  }

  if (includeNoChange) {
    values.unshift({
      key: 'noChange',
      value: translate('NoChange'),
      isDisabled: true
    });
  }

  if (includeMixed) {
    values.unshift({
      key: 'mixed',
      value: `(${translate('Mixed')})`,
      isDisabled: true
    });
  }

  if (includeSpecificBook) {
    values.push({
      key: 'specificBook',
      value: translate('OnlyThisBook')
    });
  }

  return (
    <SelectInput
      values={values}
      {...otherProps}
    />
  );
}

MonitorBooksSelectInput.propTypes = {
  includeNoChange: PropTypes.bool.isRequired,
  includeMixed: PropTypes.bool.isRequired,
  includeSpecificBook: PropTypes.bool.isRequired,
  includeMediaScopes: PropTypes.bool.isRequired,
  onChange: PropTypes.func.isRequired
};

MonitorBooksSelectInput.defaultProps = {
  includeNoChange: false,
  includeMixed: false,
  includeSpecificBook: false,
  includeMediaScopes: false
};

export default MonitorBooksSelectInput;
