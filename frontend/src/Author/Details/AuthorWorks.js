import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import SpinnerButton from 'Components/Link/SpinnerButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import { kinds } from 'Helpers/Props';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';
import { versionLabel, versionState } from './workVersions';
import styles from './AuthorWorks.css';

const STATE_CLASSES = {
  have: styles.versionHave,
  wanted: styles.versionWanted,
  none: styles.version
};

class AuthorWorks extends Component {

  constructor(props, context) {
    super(props, context);

    this.state = {
      isFetching: true,
      error: null,
      works: [],
      requestingKey: null
    };
  }

  componentDidMount() {
    this.fetchWorks();
  }

  componentDidUpdate(prevProps) {
    if (prevProps.authorId !== this.props.authorId) {
      this.fetchWorks();
    }
  }

  fetchWorks = () => {
    this.setState({ isFetching: true, error: null });

    createAjaxRequest({
      url: '/book/works',
      data: { authorId: this.props.authorId }
    }).request
      .then((works) => this.setState({ isFetching: false, works }))
      .catch((error) => this.setState({ isFetching: false, error }));
  };

  onRequestPress = (work) => {
    const source = work.versions.find((v) => !v.isVariant) || work.versions[0];

    this.setState({ requestingKey: work.workKey });

    createAjaxRequest({
      url: `/book/${source.bookId}/versions/request?searchForNewBook=true`,
      method: 'POST'
    }).request
      .always(() => {
        this.setState({ requestingKey: null });
        this.fetchWorks();
      });
  };

  render() {
    const { isFetching, error, works, requestingKey } = this.state;

    if (isFetching && !works.length) {
      return <LoadingIndicator />;
    }

    if (error) {
      return <Alert kind={kinds.DANGER}>{translate('UnableToLoadWorks')}</Alert>;
    }

    return (
      <div>
        <Alert kind={kinds.INFO}>{translate('WorksInfo')}</Alert>

        {works.map((work) => (
          <div key={work.workKey} className={styles.work}>
            <div className={styles.title}>{work.title}</div>

            <div className={styles.versions}>
              {work.versions.map((version) => (
                <span
                  key={version.bookId}
                  className={STATE_CLASSES[versionState(version)]}
                  title={version.title}
                >
                  {versionLabel(version)}
                </span>
              ))}
            </div>

            <SpinnerButton
              isSpinning={requestingKey === work.workKey}
              onPress={() => this.onRequestPress(work)}
            >
              {translate('RequestAllVersions')}
            </SpinnerButton>
          </div>
        ))}
      </div>
    );
  }
}

AuthorWorks.propTypes = {
  authorId: PropTypes.number.isRequired
};

export default AuthorWorks;
